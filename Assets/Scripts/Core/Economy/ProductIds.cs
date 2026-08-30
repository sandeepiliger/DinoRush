namespace DinoRush.Core
{
    // Section 26: "Use stable product IDs. Keep product IDs in configuration."
    //
    // These strings are a contract with Google Play once published — a product ID cannot be
    // renamed or reused after release without orphaning every existing purchase. They live in
    // one place so that a typo is a compile error rather than a silently failing purchase, and
    // so nothing else in the codebase hardcodes them.
    public static class ProductIds
    {
        public const string RemoveAds = "remove_ads";
        public const string StarterPack = "starter_pack";
        public const string CoinPackSmall = "coin_pack_small";
        public const string CoinPackMedium = "coin_pack_medium";
        public const string PremiumDino = "premium_dino";

        public static readonly string[] All =
        {
            RemoveAds,
            StarterPack,
            CoinPackSmall,
            CoinPackMedium,
            PremiumDino,
        };
    }
}
