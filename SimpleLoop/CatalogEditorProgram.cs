using System;

namespace SimpleLoop
{
    class CatalogEditorProgram
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🎭 GameWatcher Dialogue Catalog Editor");
            Console.WriteLine("=====================================");
            Console.WriteLine();
            
            try
            {
                var editor = new CatalogEditor();
                editor.ShowMainMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}