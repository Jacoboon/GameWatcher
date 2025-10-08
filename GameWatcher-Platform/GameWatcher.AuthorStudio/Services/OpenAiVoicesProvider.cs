namespace GameWatcher.AuthorStudio.Services
{
    public static class OpenAiVoicesProvider
    {
        // Curated list of OpenAI TTS voices (expand as needed)
        public static readonly string[] All = new[]
        {
            // Available voices (invalid ones removed):
            // alloy, coral, echo, fable, nova, onyx, sage, shimmer, verse
            "alloy", "coral", "echo", "fable", "nova", "onyx", "sage", "shimmer", "verse"
        };
    }
}
