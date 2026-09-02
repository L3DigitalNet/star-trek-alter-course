# 002: Prevent duplicate merged prompt constraints

## Cause

The prompt compiler duplicated constraints after merging prompt layers and could produce the phrase "a icon".

## Fix

Prompt contract v4 deduplicates merged constraints and uses kind-neutral composition grammar.
Regression coverage protects both behaviors.

## Lesson

Prompt assembly is a versioned output contract.
Test merged constraints and generated grammar at the compiler boundary, not only individual input fragments.
