using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The single call site for analytics — section 28:
    //     AnalyticsManager.TrackEvent(...)
    //
    // Beyond keeping the vendor swappable, this exists so that analytics can never be the thing
    // that breaks a run. Section 55 lists "analytics unavailable: continue normally", and the
    // easiest way to honour that is one place that swallows provider failures.
    public sealed class AnalyticsManager
    {
        private readonly IAnalyticsProvider _provider;
        private bool _sessionActive;

        public AnalyticsManager(IAnalyticsProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public int DroppedEventCount { get; private set; }

        public void Track(AnalyticsEvent value, IReadOnlyDictionary<string, object> parameters = null)
        {
            try
            {
                _provider.Track(AnalyticsEventNames.Of(value), parameters ?? EmptyParameters);
            }
            catch (Exception)
            {
                // Counted rather than silently ignored, so a provider that is failing constantly
                // is at least visible in a debug build instead of looking like it works.
                DroppedEventCount++;
            }
        }

        // Convenience for the common one- and two-parameter cases, so call sites don't each
        // build a dictionary by hand.
        public void Track(AnalyticsEvent value, string key, object parameterValue) =>
            Track(value, new Dictionary<string, object> { [key] = parameterValue });

        public void SetUserProperty(string key, string value)
        {
            try
            {
                _provider.SetUserProperty(key, value);
            }
            catch (Exception)
            {
                DroppedEventCount++;
            }
        }

        // Session bookkeeping is here rather than at call sites because double-counting
        // sessions would quietly corrupt every retention metric derived from them — and
        // Android's lifecycle makes repeated resume callbacks entirely normal.
        public void BeginSession()
        {
            if (_sessionActive) return;
            _sessionActive = true;
            Track(AnalyticsEvent.SessionStarted);
        }

        public void EndSession()
        {
            if (!_sessionActive) return;
            _sessionActive = false;
            Track(AnalyticsEvent.SessionEnded);
        }

        public bool IsSessionActive => _sessionActive;

        private static readonly IReadOnlyDictionary<string, object> EmptyParameters =
            new Dictionary<string, object>();
    }

    // Section 70's stand-in. No analytics vendor has been chosen yet (that decision is recorded
    // as open in docs/SPEC_ANALYSIS.md), so this records events in memory: enough to verify the
    // right things fire at the right moments, and enough for a debug overlay, without
    // committing the project to an SDK prematurely.
    public sealed class MockAnalyticsProvider : IAnalyticsProvider
    {
        public sealed class Entry
        {
            public string Name { get; }
            public IReadOnlyDictionary<string, object> Parameters { get; }

            public Entry(string name, IReadOnlyDictionary<string, object> parameters)
            {
                Name = name;
                Parameters = parameters;
            }
        }

        private readonly List<Entry> _events = new List<Entry>();
        private readonly Dictionary<string, string> _userProperties = new Dictionary<string, string>();

        public bool ThrowOnTrack { get; set; }

        public IReadOnlyList<Entry> Events => _events;
        public IReadOnlyDictionary<string, string> UserProperties => _userProperties;

        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (ThrowOnTrack) throw new InvalidOperationException("simulated analytics failure");
            _events.Add(new Entry(eventName, parameters));
        }

        public void SetUserProperty(string key, string value)
        {
            if (ThrowOnTrack) throw new InvalidOperationException("simulated analytics failure");
            _userProperties[key] = value;
        }

        public int CountOf(string eventName)
        {
            int count = 0;
            foreach (var entry in _events)
                if (entry.Name == eventName) count++;
            return count;
        }
    }
}
