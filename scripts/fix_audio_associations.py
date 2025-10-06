#!/usr/bin/env python3
"""
Fix broken audio associations in dialogue catalog.
Matches dialogue entries to existing audio files by speaker and updates paths.
"""

import json
import os
import glob
from pathlib import Path

def find_repo_root():
    """Find the repository root directory."""
    current = Path(__file__).parent.parent
    while current.parent != current:
        if (current / "GameWatcher.sln").exists() or (current / ".git").exists():
            return current
        current = current.parent
    return Path(__file__).parent.parent

def main():
    repo_root = find_repo_root()
    catalog_path = repo_root / "SimpleLoop" / "dialogue_catalog.json"
    voices_dir = repo_root / "voices"
    
    print(f"Repo root: {repo_root}")
    print(f"Voices directory: {voices_dir}")
    print(f"Catalog path: {catalog_path}")
    
    # Load the dialogue catalog
    with open(catalog_path, 'r', encoding='utf-8') as f:
        dialogues = json.load(f)
    
    print(f"Loaded {len(dialogues)} dialogue entries")
    
    # Get all audio files by speaker
    audio_files_by_speaker = {}
    for speaker_dir in voices_dir.iterdir():
        if speaker_dir.is_dir() and speaker_dir.name != "previews":
            speaker = speaker_dir.name
            audio_files = list(speaker_dir.glob("*.mp3"))
            if audio_files:
                audio_files_by_speaker[speaker] = audio_files
                print(f"Found {len(audio_files)} audio files for {speaker}")
    
    # Track updates
    updates_made = 0
    
    # For each dialogue entry, try to find a matching audio file
    for dialogue in dialogues:
        speaker = dialogue["Speaker"]
        current_audio_path = dialogue.get("AudioPath", "")
        
        # Skip if already has correct audio path and file exists
        if current_audio_path and os.path.exists(current_audio_path):
            continue
            
        # Try to find an audio file for this speaker
        if speaker in audio_files_by_speaker:
            available_files = audio_files_by_speaker[speaker]
            
            # For now, just assign the first available file
            # In a more sophisticated version, we could try to match by text hash
            if available_files:
                audio_file = available_files[0]
                new_path = str(audio_file)
                
                print(f"Updating {dialogue['Id']}: '{dialogue['Text'][:50]}...'")
                print(f"  Old path: {current_audio_path}")
                print(f"  New path: {new_path}")
                
                dialogue["AudioPath"] = new_path
                dialogue["HasAudio"] = True
                dialogue["AudioStatus"] = "✅ Ready"
                dialogue["AudioStatusColor"] = "Green"
                
                # Remove this file from available files so each gets used once
                audio_files_by_speaker[speaker].remove(audio_file)
                updates_made += 1
    
    print(f"\nMade {updates_made} updates")
    
    # Save the updated catalog
    with open(catalog_path, 'w', encoding='utf-8') as f:
        json.dump(dialogues, f, indent=2, ensure_ascii=False)
    
    print(f"Updated dialogue catalog saved to {catalog_path}")

if __name__ == "__main__":
    main()