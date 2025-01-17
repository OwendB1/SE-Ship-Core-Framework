namespace ShipCoreFramework
{
    public static class Settings
    {
        public static readonly bool Debug = true;

        public static readonly int
            LOG_LEVEL = 0; //messages with logPriority >= this will get logged, less than will be ignored

        public static readonly int
            CLIENT_OUTPUT_LOG_LEVEL = 3; //messages with logPriority >= this will get output to clients
    }
}