using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public sealed class ValidationResult
    {
        public static readonly ValidationResult Valid = new ValidationResult(Array.Empty<string>());

        public IReadOnlyList<string> Violations { get; }
        public bool IsValid => Violations.Count == 0;

        public ValidationResult(IReadOnlyList<string> violations)
        {
            Violations = violations ?? Array.Empty<string>();
        }
    }
}
