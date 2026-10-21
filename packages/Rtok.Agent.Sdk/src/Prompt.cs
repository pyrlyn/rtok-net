namespace Rtok.Agent.Sdk;

/// <summary>
/// Ask <paramref name="question"/> on a terminal and return the answer. Write
/// <see cref="Prompts.Line"/>, read one line and pass it to <see cref="Prompts.Answer"/>.
/// </summary>
public delegate bool Prompt(string question, bool defaultYes);
