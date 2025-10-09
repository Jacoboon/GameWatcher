# Design Documentation Index

This directory contains comprehensive design documentation for the GameWatcher V2 Platform.

## Reading Order

### For New Developers

Start here to understand the system:

1. **[APPLICATION_ARCHITECTURE.md](APPLICATION_ARCHITECTURE.md)** ⭐ **START HERE**
   - Required services per application
   - What Studio, AuthorStudio, and Runtime each do
   - DI compliance requirements

2. **[01-Architecture-Overview.md](01-Architecture-Overview.md)**
   - High-level system design
   - Component interactions
   - Technology choices

3. **[02-Game-Pack-System.md](02-Game-Pack-System.md)**
   - How game packs work
   - Creating custom packs
   - Pack discovery and loading

4. **[DI_COMPLIANCE_CHECKLIST.md](DI_COMPLIANCE_CHECKLIST.md)** ⚠️ **Reference When Editing DI**
   - Quick validation checklist
   - Required services per app
   - Common mistakes to avoid

### For Pack Authors

Focus on creating voice packs:

1. **[02-Game-Pack-System.md](02-Game-Pack-System.md)**
   - Pack structure and conventions
   - Detection configuration
   - Dialogue mapping

2. **[04-Studio-Tools-Design.md](04-Studio-Tools-Design.md)**
   - Using AuthorStudio
   - Discovery workflow
   - TTS generation

3. **[09-Voice-Previews-and-TTS.md](09-Voice-Previews-and-TTS.md)**
   - Voice configuration
   - Preview system
   - Quality control

### For Advanced Features

Implementing platform features:

4. **[03-Engine-API-Specification.md](03-Engine-API-Specification.md)**
   - Engine interfaces
   - Extension points
   - Custom implementations

5. **[05-Runtime-System.md](05-Runtime-System.md)**
   - Headless operation
   - Processing pipeline
   - Automation scenarios

6. **[06-Migration-Guide.md](06-Migration-Guide.md)**
   - V1 to V2 porting
   - Breaking changes
   - Performance considerations

7. **[08-Audio-Effects-UX.md](08-Audio-Effects-UX.md)**
   - Audio processing
   - Effects pipeline
   - User experience

## Quick Reference

### Architecture Compliance
- **Before editing any App.xaml.cs or Program.cs**: Read [APPLICATION_ARCHITECTURE.md](APPLICATION_ARCHITECTURE.md)
- **Validating DI changes**: Use [DI_COMPLIANCE_CHECKLIST.md](DI_COMPLIANCE_CHECKLIST.md)

### Creating Features
- **New game pack**: [02-Game-Pack-System.md](02-Game-Pack-System.md)
- **New engine service**: [03-Engine-API-Specification.md](03-Engine-API-Specification.md)
- **New processing stage**: [05-Runtime-System.md](05-Runtime-System.md)

### Understanding Design Decisions
- **Why three separate apps?**: [APPLICATION_ARCHITECTURE.md](APPLICATION_ARCHITECTURE.md) § Overview
- **Why game packs vs hardcoding?**: [02-Game-Pack-System.md](02-Game-Pack-System.md) § Motivation
- **Why DI everywhere?**: [01-Architecture-Overview.md](01-Architecture-Overview.md) § Dependency Injection

## Document Status

| Document | Status | Last Updated | Purpose |
|----------|--------|--------------|---------|
| APPLICATION_ARCHITECTURE.md | ✅ Current | 2025-10-08 | App responsibilities and required services |
| DI_COMPLIANCE_CHECKLIST.md | ✅ Current | 2025-10-08 | DI validation quick reference |
| 01-Architecture-Overview.md | 📝 Review | - | High-level system design |
| 02-Game-Pack-System.md | 📝 Review | - | Pack architecture |
| 03-Engine-API-Specification.md | 📝 Review | - | Engine interfaces |
| 04-Studio-Tools-Design.md | 📝 Review | - | AuthorStudio design |
| 05-Runtime-System.md | 📝 Review | - | Runtime orchestrator |
| 06-Migration-Guide.md | 📝 Review | - | V1 to V2 migration |
| 07-Studio-Authoring-Player-Modes.md | 📝 Review | - | Mode switching |
| 08-Audio-Effects-UX.md | 📝 Review | - | Audio processing |
| 09-Voice-Previews-and-TTS.md | 📝 Review | - | TTS and previews |

**Legend:**
- ✅ Current: Recently updated and validated
- 📝 Review: Needs review for V2 accuracy
- ⚠️ Outdated: Requires significant updates

## Contributing to Documentation

When adding or updating design docs:

1. **Update this index** with the new document
2. **Add to reading order** if it's foundational
3. **Update status table** with current date
4. **Cross-reference** related documents
5. **Add version history** section to the doc itself

## Questions?

- **Architecture questions**: Check APPLICATION_ARCHITECTURE.md first
- **Implementation questions**: See Engine API Specification
- **Pack creation questions**: See Game Pack System
- **Still stuck?**: Check parent [AGENTS.md](../../AGENTS.md) for conventions
