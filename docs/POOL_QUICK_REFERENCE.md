# ObjectPool Modernization - Quick Reference Card

## The 30-Second Summary

**What**: Modern container for managing Objects with better API and zero breaking changes
**Status**: ✅ Ready to use
**Risk**: 🟢 Very low (Phase 1)
**Effort**: 🟡 Medium (each function takes ~10 minutes)
**Benefit**: 📈 Cleaner code, slight performance improvement

---

## Quick Start

### 1. Include the Header
```cpp
#include "object_pool.h"
```

### 2. Replace Your Loop
```cpp
// BEFORE
for (int i = 0; i < ActiveObjectCount; ++i) {
    Object &object = Objects[ActiveObjects[i]];
    // ... use object
}

// AFTER
for (Object &object : gObjectPool) {
    // ... use object (same object, simpler code)
}
```

### 3. Test & Commit
```bash
# Compile
cmake --build build

# Test
# Load level, interact with objects, save/load

# Commit
git add .
git commit -m "Refactor: use ObjectPool for iteration in SomeFunction"
```

That's it! You've just modernized your code.

---

## What's Available

### Pool API
```cpp
gObjectPool                    // The global pool instance

gObjectPool.activeCount()      // How many objects are active
gObjectPool[i]                 // Access object at index i
gObjectPool[i]._otype          // Use objects like always

for (Object &obj : gObjectPool) {  // Iterate all active objects
    // ...
}
```

### Legacy Still Works
```cpp
Objects[i]                     // Still works (same data)
ActiveObjects[...]             // Still works (same data)
ActiveObjectCount              // Still works (same value)
```

### Both Are Equivalent
```
Pool way:    for (Object &obj : gObjectPool) { ... }
Legacy way:  for (int i = 0; i < ActiveObjectCount; ++i) { ... }
Result:      Same objects, same order, both 100% compatible
```

---

## Why This Matters

### Before (Legacy Pattern)
```cpp
for (int i = 0; i < ActiveObjectCount; ++i) {
    Object &object = Objects[ActiveObjects[i]];
    // 3 different concepts to track:
    // - ActiveObjectCount (how many)
    // - ActiveObjects (which indices)
    // - Objects (the data)
}
```

### After (Modern Pattern)
```cpp
for (Object &object : gObjectPool) {
    // 1 simple concept:
    // - gObjectPool (has everything)
}
```

### Benefits
✅ Less indirection (fewer array lookups)
✅ Clearer intent (for each active object)
✅ Type-safe (can't mix up which array)
✅ STL-idiomatic (range-for loop)
✅ Compiler-friendly (better optimization)

---

## Three Migration Levels

### Level 1: Just Read Objects 🟢 (Easy)
```cpp
// Current phase - safe, low risk
for (Object &obj : gObjectPool) {
    if (obj.IsTrap()) {
        HandleTrap(obj);
    }
}
```
✅ Safe to iterate
✅ Safe to read fields
✅ Safe to modify object fields
❌ Don't allocate/deallocate

### Level 2: Synchronized Allocation 🟡 (Medium - Future)
```cpp
// When we do Phase 2
Object *obj = gObjectPool.allocate();  // Use pool allocation
if (obj) {
    *obj = NewObject;
}
```
⏸️ Not yet implemented
✅ Will preserve active count sync
❌ May have allocation overhead

### Level 3: Full Integration 🔴 (Hard - Future)
```cpp
// When we're fully modernized
// Only use pool, no legacy arrays
// All code migrated
```
⏸️ Year+ away
✅ Fully type-safe
❌ Requires extensive refactoring

---

## The Migration Path

```
┌──────────────────────────────────────────────────────────┐
│ Phase 1: Read-Only Iteration (THIS PHASE - SAFE)        │
│                                                          │
│ ✅ Convert loop: for (...) => for (...: gObjectPool)   │
│ ✅ Use pool iterator                                    │
│ ✅ Zero breaking changes                               │
│ ✅ Easy to test & verify                               │
│                                                          │
│ Timeline: 2-4 weeks if going smoothly                  │
└──────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│ Phase 2: Synchronized Allocation (2-3 MONTHS OUT)      │
│                                                          │
│ ⏸️ Not yet implemented                                  │
│ • Update AddObject() to use pool                       │
│ • Keep ActiveObjectCount in sync                       │
│ • Both systems produce identical results               │
└──────────────────────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────┐
│ Phase 3: Full Integration (6-12 MONTHS OUT)            │
│                                                          │
│ ⏸️ Not yet planned                                      │
│ • Pool becomes primary storage                         │
│ • Legacy arrays become compatibility layer             │
│ • Full modernization complete                          │
└──────────────────────────────────────────────────────────┘
```

---

## How to Know If You Should Migrate a Function

### ✅ YES - Good Candidates
- [ ] Function loops through `ActiveObjectCount`
- [ ] Loop is read-only (no allocation/deletion)
- [ ] Function runs frequently (game loop, rendering, etc.)
- [ ] You understand what the function does
- [ ] There are tests for the function

### ❌ NO - Wait For Later
- [ ] Function allocates/deletes objects (wait for Phase 2)
- [ ] Function is rarely called (not worth effort)
- [ ] Function is too complex to understand (understand first, then migrate)
- [ ] No tests exist for the function (add tests first)

---

## Common Questions

**Q: Will this break my save files?**
A: No. Binary format is identical. Save files work perfectly.

**Q: Will multiplayer break?**
A: No. Object sync format is unchanged. Fully compatible.

**Q: Is there a performance cost?**
A: No. Pool is as fast or faster (fewer indirections).

**Q: How do I test my changes?**
A: Load level, interact with objects, save and load.

**Q: What if something breaks?**
A: Revert the commit. Legacy code still works!

**Q: How long does one migration take?**
A: 10-20 minutes per function (read, modify, test, commit).

**Q: Should I convert everything at once?**
A: No. Do one function per commit. Safer and easier to debug.

**Q: What if I make a mistake?**
A: No worries. Undo, fix, resubmit. Low stakes!

---

## The Pattern You'll Use

This is what you'll repeat many times:

```
1. Find function with loop:
   for (int i = 0; i < ActiveObjectCount; ++i) {
       Object &object = Objects[ActiveObjects[i]];

2. Replace with:
   for (Object &object : gObjectPool) {

3. Compile: cmake --build build

4. Test: Load level, interact, save/load

5. Commit: git commit -m "Refactor: use pool in Function()"

6. Done! 🎉
```

**Total time per function: ~15 minutes**
**Total benefit: Cleaner code + small perf boost**

---

## Your First Migration (Right Now)

### The Easiest Function to Start With
Look for this pattern in `Source/objects.cpp`:

```cpp
void SomeFunction()
{
    for (int i = 0; i < ActiveObjectCount; ++i) {
        Object &object = Objects[ActiveObjects[i]];
        // Only reading object fields, not modifying them
        if (object._otype == OBJ_NULL) continue;
        // ... do something
    }
}
```

### Your Challenge
1. Open `Source/objects.cpp`
2. Find a function matching above pattern
3. Replace the loop: `for (int i = 0; i < ActiveObjectCount; ++i) { Object &object = Objects[ActiveObjects[i]];`
   with: `for (Object &object : gObjectPool) {`
4. Build: `cmake --build build`
5. Test: Load a level, interact with objects
6. Commit: `git commit -m "Refactor: use ObjectPool iterator in SomeFunction()"`

**Estimated time: 20 minutes**
**Difficulty: Easy**
**Risk: Very Low**

---

## Documentation Map

**I want to...**
- ...understand the architecture → Read `POOL_IMPLEMENTATION_SUMMARY.md`
- ...convert my first function → Read `POOL_FIRST_STEPS.md`
- ...see code examples → Read `POOL_CODE_EXAMPLES.md`
- ...learn the full API → Read `object_pool.h` (well-commented)
- ...understand design decisions → Read `ENTITY_POOL_ANSWERS.md`
- ...get started right now → You're reading the right doc!

---

## Success Checklist (For Each Function You Convert)

- [ ] Function identified (has the loop pattern)
- [ ] Loop replaced with pool iterator
- [ ] Code compiles without warnings
- [ ] Game loads normally
- [ ] Can interact with objects
- [ ] Can save the game
- [ ] Can load the game
- [ ] Behavior is identical to before
- [ ] Commit message is clear
- [ ] Pushed to branch

---

## The Big Picture

You're participating in a **gradual, low-risk modernization** of the codebase.

Each migration is:
- ✅ Small (one loop changed)
- ✅ Safe (no logic changes)
- ✅ Testable (immediate verification)
- ✅ Reversible (git revert if needed)
- ✅ Visible (cleaner code)

Together they add up to a **measurably better codebase** with:
- Cleaner abstractions
- Better type safety
- Slightly better performance
- Easier to maintain
- Easier to extend

---

## Get Started!

**Next Step**: Read `POOL_FIRST_STEPS.md` for detailed first migration guide.

**Questions?** All answers are in the documentation suite.

**Ready?** Pick a function and start migrating! 🚀

---

**Remember**: This is a marathon, not a sprint. Enjoy the process of improving the codebase one function at a time!

