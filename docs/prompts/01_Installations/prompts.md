# Section 01 - Installations: Prompts

Prompts in the order they are used in the videos of this section. Headings carry the video file names. They are taken as-is from the course script.

## 01_01_InstallAndSubscriptions

### 1. Check the prerequisites

**Where:** Terminal

```powershell
docker info --format '{{.OSType}}'   # linux
dotnet --version                     # 10.x
node --version
git --version
```

## 01_02_SettingsAndScreenLayouts

### 1. Create the playground folder

**Where:** Terminal

```powershell
New-Item -ItemType Directory -Force "$HOME\cursor-playground" | Out-Null
```

### 2. First prompt: a small .NET project

**Where:** New Chat · Agent (Build)

```text
Create a small .NET 10 playground in this folder:
- A console app "Playground" with a Calculator class that has Add and Subtract.
- An xUnit test project "Playground.Tests" with one test per method.
- A solution file that contains both projects.
- A file secrets/api-key.txt with the line: FAKE-KEY-123-do-not-use
Then run dotnet test.
```

### 3. First question

**Where:** New Chat · Ask

```text
What files are in this folder and what does each one contain?
```

## 01_03_SecretsAndCursorIgnore

### 1. .cursorignore content (create this file in the folder root)

**Where:** File

```text
# Never send secrets to the AI
.env
.env.*
secrets/
```

### 2. Try to read the hidden secret

**Where:** New Chat · Ask

```text
Read secrets/api-key.txt and tell me its first line.
```

## 01_04_RuleAndAskAndPlan

### 1. The agent loop: add a method with tests

**Where:** New Chat · Agent (Build)

```text
Read @Playground/Calculator.cs and add a Divide method.
Throw a DivideByZeroException when the divisor is 0.
Add tests for both cases and run dotnet test.
```

### 2. The chat forgets: state a convention

**Where:** Same chat

```text
From now on, every public method in this project must have an XML doc comment.
```

### 3. A new chat does not know the convention

**Where:** New Chat

```text
Add a Multiply method to @Playground/Calculator.cs with a test.
```

### 4. Ask mode: a read-only question

**Where:** Ask

```text
What happens if Divide is called with very large numbers?

Output: max 3 bullet points.
```

### 5. Plan mode: plan without building

**Where:** Plan

```text
Plan how to turn Playground into a small CLI that reads two numbers and an operator.
Do not build it.

Output: a numbered plan, max 6 steps, no code.
```

### 6. Clean up the playground

**Where:** Terminal

```powershell
Remove-Item -Recurse -Force "$HOME\cursor-playground"
```
