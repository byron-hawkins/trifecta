*author: Copilot*

# Testing rules

1. If a test exists in both `test/macarena/...` and `test/toddler/...`, all
   requests for that test implicitly refer to the one in `test/macarena/...`.
2. Never delete a test without specific instruction to do so. Even if obsolete
   tests accumulate in `test/toddler/`, they are required for project
   submission.
3. Always generate tests to use the framework in `test/common/`.
4. Macarena tests may mock components to exercise specific test semantics.
   Do not add mocks merely to compensate for missing dependencies or other
   problems that need to be resolved in `src/`.
