using System;

namespace SimpleLoop.Test
{
    class FuzzyMatchTest
    {
        static void Main()
        {
            Console.WriteLine("=== Testing Fuzzy Matching Logic ===\n");
            
            // Test Case 1: OCR garbage at end
            var existing1 = "I just don't know what we can do... please help our prince!";
            var ocr1 = "I just don't know what we can do... help our prince! p I e,eSe";
            TestMatch(existing1, ocr1, "Case 1: OCR garbage");
            
            // Test Case 2: OCR character corruption
            var existing2 = "Weapons and armor made of mythril are sturdy and powerful. You should give them a try. You'll be surprised!";
            var ocr2 = "Ijeapons and armor made of mythril are sturdy and powerful. You should give them a try. Yau' II be surprised!";
            TestMatch(existing2, ocr2, "Case 2: OCR corruption");
            
            // Test Case 3: Should NOT match (genuinely different)
            var existing3 = "I shall wait patiently until then.";
            var different = "Welcome to my shop, traveler!";
            TestMatch(existing3, different, "Case 3: Different dialogue");
        }
        
        static void TestMatch(string existing, string newText, string testName)
        {
            Console.WriteLine($"--- {testName} ---");
            Console.WriteLine($"Existing: '{existing}'");
            Console.WriteLine($"New OCR:  '{newText}'");
            
            var isMatch = IsCloseMatch(existing, newText);
            Console.WriteLine($"Result: {(isMatch ? "✅ MATCH" : "❌ NO MATCH")}");
            Console.WriteLine();
        }
        
        static bool IsCloseMatch(string existing, string newText)
        {
            if (string.IsNullOrEmpty(existing) || string.IsNullOrEmpty(newText)) return false;
            
            // Normalize both texts for comparison
            var text1 = NormalizeForComparison(existing);
            var text2 = NormalizeForComparison(newText);
            
            // If normalized texts are identical, it's a match
            if (text1.Equals(text2, StringComparison.OrdinalIgnoreCase)) return true;
            
            // Calculate similarity using Levenshtein distance
            var similarity = CalculateSimilarity(text1, text2);
            
            // Consider it a match if similarity is very high (95%+ for dialogue)
            const double SIMILARITY_THRESHOLD = 0.95;
            bool isMatch = similarity >= SIMILARITY_THRESHOLD;
            
            Console.WriteLine($"   Normalized 1: '{text1}'");
            Console.WriteLine($"   Normalized 2: '{text2}'");
            Console.WriteLine($"   Similarity: {similarity:P1} (threshold: {SIMILARITY_THRESHOLD:P1})");
            
            return isMatch;
        }
        
        static string NormalizeForComparison(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            
            var normalized = text.Trim();
            
            // Remove common OCR punctuation variations
            normalized = normalized.TrimEnd('.', ',', '!', '?', ';', ':');
            
            // Normalize whitespace (multiple spaces -> single space)
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
            
            // Remove common OCR artifacts
            normalized = normalized.Replace("  ", " ");  // Double spaces
            normalized = normalized.Replace(" .", ".");   // Space before period
            normalized = normalized.Replace(" ,", ",");   // Space before comma
            normalized = normalized.Replace(" !", "!");   // Space before exclamation
            normalized = normalized.Replace(" ?", "?");   // Space before question mark
            
            // Normalize smart quotes and apostrophes
            normalized = normalized.Replace("'", "'").Replace("'", "'");
            normalized = normalized.Replace(""", "\"").Replace(""", "\"");
            
            return normalized.Trim();
        }
        
        static double CalculateSimilarity(string text1, string text2)
        {
            if (text1 == text2) return 1.0;
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2)) return 0.0;
            
            var maxLength = Math.Max(text1.Length, text2.Length);
            if (maxLength == 0) return 1.0;
            
            var distance = CalculateLevenshteinDistance(text1, text2);
            return 1.0 - (double)distance / maxLength;
        }
        
        static int CalculateLevenshteinDistance(string s1, string s2)
        {
            var len1 = s1.Length;
            var len2 = s2.Length;
            
            if (len1 == 0) return len2;
            if (len2 == 0) return len1;
            
            var matrix = new int[len1 + 1, len2 + 1];
            
            // Initialize first row and column
            for (int i = 0; i <= len1; i++) matrix[i, 0] = i;
            for (int j = 0; j <= len2; j++) matrix[0, j] = j;
            
            // Calculate distances
            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    
                    matrix[i, j] = Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1,      // Deletion
                            matrix[i, j - 1] + 1),     // Insertion
                        matrix[i - 1, j - 1] + cost   // Substitution
                    );
                }
            }
            
            return matrix[len1, len2];
        }
    }
}