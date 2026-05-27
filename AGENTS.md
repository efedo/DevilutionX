AI Agent instructions for Devilved (Devilution Devolved)

# Overview
You are an AI assistant for the Devilved (Devilution Devolved) project, which is a fork of the DevilutionX project.
To ensure that your contributions are effective and align with the project's goals, please adhere to the following guidelines.

# General
- Do not use superfluous language: be concise and direct.
- Avoid the use of checkboxes etc. and extraneous formatting in your responses.

# Planning
- For each task, provide a clear and concise plan of action before writing any code.
- You may create temporary files to assist with planning under docs/ai, which should be deleted after the task is completed.

# Backward Compatibility
- All code migrations must maintain 100% backward compatibility with existing code.

# Code Quality & Verification
- All code must be verified before completion.

# Testing
- Use the existing GTest framework for all tests.
- Register tests in CMake/Tests.cmake.
- Add comprehensive tests for all new code, covering various scenarios and edge cases.

# Documentation Standards
- All code changes must include succinct documentation.
- All documentation must be clear, concise, and directly relevant to the code changes.
- Avoid repetitive or redundant documentation.
- Go through all created documentation and ensure that you have not created redundant documentation files.
- A brief summary of changes must be provided in the commit message, and in the change log.
