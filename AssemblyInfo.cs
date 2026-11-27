using System.Runtime.CompilerServices;
using System.Windows;

// the test project's renderer reads named elements of the note window (they are internal)
[assembly: InternalsVisibleTo("SmartNotes.Tests")]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]
