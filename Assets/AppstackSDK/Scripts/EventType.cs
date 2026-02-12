namespace Appstack
{
    /// <summary>
    /// Standard attribution events supported by the SDK.
    /// Values follow SNAKE_CASE notation used by mobile measurement partners (MMPs).
    /// </summary>
    public enum EventType
    {
        // Lifecycle
        INSTALL,

        // Authentication & account
        LOGIN,
        SIGN_UP,
        REGISTER,

        // Monetization
        PURCHASE,
        ADD_TO_CART,
        ADD_TO_WISHLIST,
        INITIATE_CHECKOUT,
        START_TRIAL,
        SUBSCRIBE,

        // Games / progression
        LEVEL_START,
        LEVEL_COMPLETE,

        // Engagement
        TUTORIAL_COMPLETE,
        SEARCH,
        VIEW_ITEM,
        VIEW_CONTENT,
        SHARE,

        // Catch-all
        CUSTOM
    }
}
