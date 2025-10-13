# TTS Enhancement Strategies for GameWatcher
**Date:** October 10, 2025  
**Context:** Improving OpenAI TTS voice line quality through text preprocessing

**🎉 BREAKTHROUGH UPDATE:** The `gpt-4o-mini-tts` model **DOES support an `instructions` parameter!** This was discovered through testing in the OpenAI Playground. The traditional `tts-1` and `tts-1-hd` models do not have this feature, but `gpt-4o-mini-tts` does - making it the superior choice for GameWatcher.

---

## 🎯 The Challenge ~~& Discovery~~

~~OpenAI's TTS API (`/v1/audio/speech`) accepts only **raw text** - no prompt engineering, no context, no emotion guidance.~~

**UPDATE:** This was TRUE for `tts-1` and `tts-1-hd`, but **NOT TRUE** for `gpt-4o-mini-tts`!

**Current Implementation:**
```csharp
// OLD (limited)
input = "The LIGHT WARRIORS have arrived."  // Bare text only

// NEW (with instructions - WORKING!)
{
  "model": "gpt-4o-mini-tts",
  "voice": "onyx",
  "input": "The kingdom is in grave danger.",
  "instructions": "Wise old king speaking gravely. Add 1 second of silence at the end."
}
```

**Test Results:** Instructions parameter WORKS! Tested with:
- ✅ Character context: "Wise old king speaking gravely"
- ✅ Timing control: "Add 1 second of silence"
- ✅ Emotion guidance: Instructions influence prosody and delivery
- ✅ NO spoken artifacts: Context is NOT read aloud, only used for acting guidance

---

## 💡 IMPLEMENTED Solution: Instructions Parameter API

### ✅ Strategy: Direct Instructions via API (WINNER!)

**Status:** ✅ **IMPLEMENTED** in `OpenAiTtsService.cs` and `TtsInstructionsBuilder.cs`

**Concept:** Use the `instructions` parameter to provide rich context to gpt-4o-mini-tts

**Examples:**
```csharp
// Example 1: Role-based
var instructions = TtsInstructionsBuilder.Build(
    speakerRole: "King",
    emotion: "worried"
);
// Result: "Speak with regal authority and wisdom, like a noble king addressing subjects. Speak with anxious concern."

// Example 2: Audition Style (user's original brilliant idea!)
var instructions = TtsInstructionsBuilder.BuildAuditionStyle(
    speakerName: "King of Corneria",
    speakerRole: "King",
    text: "The kingdom is in grave danger.",
    emotion: "worried"
);
// Result: "You are a voice actor auditioning for the role of King. Your line is: \"The kingdom is in grave danger.\". The emotion is worried. Perform the line with conviction, emotion, and emphasis to land the part."

// Example 3: With timing control
var instructions = TtsInstructionsBuilder.Build("King", "worried");
instructions = TtsInstructionsBuilder.WithPause(instructions, secondsAtEnd: 1.0);
// Result: "Speak with regal authority... Add 1.0 seconds of silence at the end."
```

---

## 💡 Solution Strategies

### Strategy 1: Punctuation Engineering ⚡ (EASY - 15 mins)

**Concept:** Use punctuation to guide pacing and emotion

**Examples:**
```csharp
// Original
"The LIGHT WARRIORS have arrived."

// Enhanced
"The LIGHT WARRIORS... have arrived!"  // Dramatic pause + excitement

// Original
"Princess Sara needs our help."

// Enhanced  
"Princess Sara—she needs our help... now!"  // Urgency + concern
```

**Implementation:**
```csharp
public static string EnhancePunctuation(string text, string emotionHint = null)
{
    // Add dramatic pauses before important nouns
    text = Regex.Replace(text, @"\b(LIGHT WARRIORS|Princess|King|Chaos)\b", 
        match => $"... {match.Value}");
    
    // Convert periods to exclamations for urgent/excited dialogue
    if (emotionHint == "urgent" || emotionHint == "excited")
    {
        text = text.Replace(".", "!");
    }
    
    // Add em-dashes for interruptions or emphasis
    if (emotionHint == "concerned" || emotionHint == "worried")
    {
        text = text.Replace(",", "—");
    }
    
    return text;
}
```

**Pros:**
- ✅ Works immediately with current TTS
- ✅ No API changes
- ✅ Simple to implement

**Cons:**
- ⚠️ Limited control
- ⚠️ Can look odd in text-based logs

---

### Strategy 2: Contextual Text Injection 🎭 (MEDIUM - 30 mins)

**Concept:** Prepend invisible context that influences TTS prosody

**Examples:**
```csharp
// Original
speaker: "King"
text: "The kingdom is in grave danger."

// Enhanced (hidden context)
input: "[Wise old king speaking gravely] The kingdom is in grave danger."
```

**Implementation:**
```csharp
public static string AddSpeakerContext(string text, string speakerName, string speakerRole)
{
    var contextMap = new Dictionary<string, string>
    {
        ["King"] = "[Wise old king speaking with authority]",
        ["Princess Sara"] = "[Young worried princess speaking softly]",
        ["Garland"] = "[Evil knight speaking menacingly]",
        ["Sage"] = "[Ancient sage speaking mysteriously]",
        ["Villager"] = "[Ordinary citizen speaking plainly]",
    };
    
    var context = contextMap.GetValueOrDefault(speakerRole, $"[{speakerName} speaking]");
    return $"{context} {text}";
}
```

**Testing Required:**
- Does OpenAI TTS actually read the context out loud? (BAD)
- Or does it influence prosody without being spoken? (GOOD)
- We need to test this!

**Pros:**
- ✅ Rich context per speaker
- ✅ Easy to customize per character

**Cons:**
- ⚠️ Might get spoken aloud (need testing)
- ⚠️ Adds token cost if context is long

---

### Strategy 3: Emotion Tags in Text 😊 (MEDIUM - 40 mins)

**Concept:** Use natural language emotion markers

**Examples:**
```csharp
// Original
"Thank you for saving me."

// Enhanced
"Thank you so much for saving me!" // Gratitude
"*gasps* Thank you for saving me..." // Shocked
"Thank you... for saving me. *sobs*" // Emotional
```

**Implementation:**
```csharp
public class EmotionEnhancer
{
    private static readonly Dictionary<string, Func<string, string>> EmotionStrategies = new()
    {
        ["excited"] = text => $"{text.TrimEnd('.')}!",
        ["shocked"] = text => $"*gasps* {text}",
        ["sad"] = text => $"{text}... *sobs*",
        ["angry"] = text => $"{text.TrimEnd('.')}! *growls*",
        ["mysterious"] = text => $"Hmm... {text}...",
        ["fearful"] = text => $"*trembling* {text}!",
        ["confident"] = text => $"Ha! {text}",
    };
    
    public static string Enhance(string text, string emotion)
    {
        if (string.IsNullOrEmpty(emotion)) return text;
        
        return EmotionStrategies.TryGetValue(emotion.ToLower(), out var strategy)
            ? strategy(text)
            : text;
    }
}
```

**Pros:**
- ✅ Natural language approach
- ✅ Clear visual indicators
- ✅ Works with TTS prosody

**Cons:**
- ⚠️ Sound effects (*gasps*, *sobs*) might be spoken literally
- ⚠️ Requires emotion tagging in dialogue data

---

### Strategy 4: SSML Markup 🏗️ (ADVANCED - 60 mins + TESTING)

**Concept:** Use Speech Synthesis Markup Language

**Note:** OpenAI TTS docs don't mention SSML support, but worth testing!

**Examples:**
```xml
<speak>
  <emphasis level="strong">The LIGHT WARRIORS</emphasis>
  <break time="500ms"/>
  have arrived.
</speak>
```

**Implementation:**
```csharp
public static string ToSSML(string text, Dictionary<string, string> hints)
{
    var sb = new StringBuilder("<speak>");
    
    // Emphasize important words
    text = Regex.Replace(text, @"\b([A-Z\s]{2,})\b", 
        match => $"<emphasis level=\"strong\">{match.Value}</emphasis>");
    
    // Add pauses at punctuation
    text = text.Replace("...", "<break time=\"500ms\"/>");
    text = text.Replace("—", "<break time=\"300ms\"/>");
    
    // Prosody for emotion
    if (hints.TryGetValue("emotion", out var emotion))
    {
        var rate = emotion == "excited" ? "fast" : "medium";
        var pitch = emotion == "excited" ? "+5%" : "0%";
        text = $"<prosody rate=\"{rate}\" pitch=\"{pitch}\">{text}</prosody>";
    }
    
    sb.Append(text);
    sb.Append("</speak>");
    return sb.ToString();
}
```

**Testing Required:**
- ✅ Test if OpenAI TTS supports SSML
- ✅ Test if it strips tags or errors
- ✅ Compare output quality

**Pros:**
- ✅ Industry standard
- ✅ Fine-grained control
- ✅ Widely supported

**Cons:**
- ⚠️ **OpenAI TTS may not support this** (needs testing!)
- ⚠️ Complex to implement
- ⚠️ Requires dialogue schema changes

---

### Strategy 5: Speaker-Specific Voice Customization 🎤 (EASY - 20 mins)

**Concept:** Better voice matching per character role

**Current:** All characters can use any voice  
**Enhanced:** Automatic voice selection based on character archetype

**Implementation:**
```csharp
public static class VoiceSelector
{
    private static readonly Dictionary<string, string[]> RoleVoices = new()
    {
        // Authority figures (deep, commanding)
        ["King"] = new[] { "onyx", "fable", "echo" },
        ["Knight"] = new[] { "onyx", "echo" },
        ["Warrior"] = new[] { "echo", "onyx" },
        
        // Female characters (varied)
        ["Princess"] = new[] { "nova", "shimmer", "coral" },
        ["Queen"] = new[] { "fable", "sage" },
        ["Villager_F"] = new[] { "coral", "nova" },
        
        // Wise/Old characters
        ["Sage"] = new[] { "fable", "sage", "onyx" },
        ["Elder"] = new[] { "sage", "fable" },
        
        // Villains (dramatic)
        ["Garland"] = new[] { "onyx" },
        ["Lich"] = new[] { "echo", "onyx" },
        ["Chaos"] = new[] { "onyx" },
        
        // Generic NPCs
        ["Villager"] = new[] { "alloy", "coral", "nova", "shimmer" },
    };
    
    public static string SelectBestVoice(string speakerName, string speakerRole)
    {
        // Try role-based selection
        if (!string.IsNullOrEmpty(speakerRole) && 
            RoleVoices.TryGetValue(speakerRole, out var voices))
        {
            return voices[0]; // Return best match
        }
        
        // Fallback: name-based heuristics
        if (speakerName.Contains("King")) return "fable";
        if (speakerName.Contains("Princess")) return "nova";
        if (speakerName.Contains("Sage")) return "sage";
        
        // Default
        return "alloy";
    }
}
```

**Pros:**
- ✅ Immediate quality improvement
- ✅ Consistent character voices
- ✅ No API changes

**Cons:**
- ⚠️ Requires character role metadata
- ⚠️ May need per-game tuning

---

### Strategy 6: Speed Variation for Emotion ⚡ (EASY - 10 mins)

**Concept:** Adjust `speed` parameter based on dialogue context

**Implementation:**
```csharp
public static double CalculateOptimalSpeed(string text, string emotion)
{
    return emotion switch
    {
        "excited" or "urgent" or "panicked" => 1.2,  // Faster
        "sad" or "reflective" or "mysterious" => 0.9,  // Slower
        "calm" or "wise" => 0.95,  // Slightly slower
        "angry" => 1.1,  // Slightly faster
        _ => 1.0  // Normal
    };
}
```

**Pros:**
- ✅ Simple to implement
- ✅ Supported by OpenAI TTS
- ✅ Noticeable difference

**Cons:**
- ⚠️ Limited control
- ⚠️ Requires emotion tagging

---

## 🎯 Recommended Implementation Plan

### Phase 1: Quick Wins (30 mins)
1. ✅ **Punctuation Engineering** - Add dramatic pauses and emphasis
2. ✅ **Voice Selection** - Auto-select best voice per character role
3. ✅ **Speed Variation** - Adjust speed based on emotion hints

### Phase 2: Testing (60 mins)
4. 🔬 **Test Contextual Injection** - Does "[King speaking]" work without being spoken?
5. 🔬 **Test SSML** - Does OpenAI TTS support SSML tags?
6. 🔬 **Test Emotion Tags** - Do *gasps*, *sobs*, etc. sound natural?

### Phase 3: Advanced (120 mins)
7. 🚀 **Implement Best Strategy** - Based on test results
8. 🚀 **UI Controls** - Let pack authors choose emotion/context per line
9. 🚀 **Preset Library** - Common emotion presets (excited, sad, mysterious, etc.)

---

## 🧪 Testing Script

```csharp
public class TtsEnhancementTester
{
    public async Task RunTests()
    {
        var testCases = new[]
        {
            // Test 1: Bare text (baseline)
            new { Name = "Baseline", Text = "The LIGHT WARRIORS have arrived." },
            
            // Test 2: Punctuation engineering
            new { Name = "Punctuation", Text = "The LIGHT WARRIORS... have arrived!" },
            
            // Test 3: Context injection
            new { Name = "Context", Text = "[Wise king speaking gravely] The LIGHT WARRIORS have arrived." },
            
            // Test 4: Emotion tags
            new { Name = "Emotion", Text = "*excited* The LIGHT WARRIORS have arrived!" },
            
            // Test 5: SSML
            new { Name = "SSML", Text = "<speak><emphasis>The LIGHT WARRIORS</emphasis> have arrived.</speak>" },
        };
        
        foreach (var test in testCases)
        {
            Console.WriteLine($"\n🎤 Testing: {test.Name}");
            Console.WriteLine($"Input: {test.Text}");
            
            var outputPath = $"test_{test.Name}.mp3";
            await GenerateTtsAsync(test.Text, "fable", 1.0, outputPath);
            
            Console.WriteLine($"✅ Generated: {outputPath}");
            Console.WriteLine("Listen and rate quality (1-10): ");
            // Play and rate
        }
    }
}
```

---

## 📝 Dialogue Schema Enhancement

To support emotion/context, extend dialogue model:

```csharp
public class DialogueEntry
{
    public string Text { get; set; }
    public string Speaker { get; set; }
    
    // NEW: Enhancement fields
    public string? Emotion { get; set; }  // "excited", "sad", "angry", etc.
    public string? SpeakerRole { get; set; }  // "King", "Princess", "Sage"
    public double? SpeedOverride { get; set; }  // Optional speed (0.25-4.0)
    public string? ContextHint { get; set; }  // "[speaking softly]", etc.
}
```

---

## 🎬 Conclusion

**Bottom Line:** While we can't directly prompt OpenAI TTS like GPT, we CAN:
1. ✅ Engineer the input text for better prosody
2. ✅ Select optimal voices per character
3. ✅ Adjust speed for emotional context
4. 🔬 **TEST** if context injection works without being spoken
5. 🔬 **TEST** if SSML is supported

**Your Original Idea Was Brilliant!** The "voice actor auditioning" concept would be PERFECT if OpenAI exposed a prompt parameter. Since they don't, we adapt by making the text itself carry that context.

**Next Steps:**
1. Run the testing script to find what works
2. Implement the winning strategies
3. Add UI controls in AuthorStudio for emotion/context
4. Profit from much better voice acting! 🎭
