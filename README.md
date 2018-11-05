# TNT - The .NET Translation Tool

A command line tool for managing translations based on strings extracted from .NET assemblies. Supports XLIFF roundtrips and machine translations.

Born out of the emotional rejection of .NET resources files and their identifiers spitting all over my code. 

Then, over hundred commits later, `tnt` seems to get somehat useful. All the interesting decisions that had to be made, programming in F#, and the experience with how good machine translations can be today, were well worth every commit and the many hours I put in.

If you want to use resource files for string translations after you read this, please leave a note.

## Installation

`dotnet tool install tnt-cli -g --add-source https://www.myget.org/F/pragmatrix/api/v2`

Yes, `tnt` is a new fancy `dotnet` command line tool.

## Concepts & Walkthrough

### Projects & Initialization & Subdirectories

`tnt` likes to work in your project's directory, the best directory is the directory of your application's project. Change into this directory an initialize `tnt` with `tnt init`. This creates the subdirectory `.tnt` and the file `.tnt/sources.json`. The `.tnt` directory contains all the valuable information `tnt` is managing for you: these are the list of sources and the translation files, which means that this directory should be checked in into your repository. `tnt` may be somewhat unforgiving, so this is not a recommendation. If you want to use `tnt`, check in the `.tnt` directory and all its contents.

> `tnt init` creates a default source language `en-US`: this is the language your original strings are in. If you use a different source language, you can change it anytime with `tnt init -l [your language tag]`.

### Sources

A source is something `tnt` retrieves original strings from. Currently, `tnt` supports only .NET assemblies as sources.

All sources are listed in the file `.tnt/sources.json` and can be added by invoking `tnt add -a [relative path to the assembly]` in your project's directory. For example `tnt add -a bin/Release/netcoreapp2.1/MyAwesomeApp.exe` would add this assembly to the list of sources.

> `tnt` does not read or modify any of your project or other files, it only accesses the sources, you specify.

### Language & Assembly Extraction

`tnt` extracts tagged strings out of .NET assemblies. For C#, each string that need to be translated is appended with a function invocation `.t()` that does two things. First, it tags the string that comes before it, and second, `t()` actually tries to translate the string if there exists a suitable translation available (I'll come to that later). 

To make the `.t()` function available to your projects, add the [TNT.T][TNT.T] NuGet to your project and add a `using TNT;` on top of the files you wish to use tag strings and tag some. Lucky owners of Resharper may just type in `.t()` after a string and add the `using TNT;` by pressing ALT+Enter.

Before the extraction begins, you need to add at least one target language to the project. Add one, say Spanisch with `tnt add -l Spanish`. Here you can either use language names or language tags, if you are not sure what to use, `tnt` can show you a list of all .NET language tags and related language names with `tnt show languages`. 

Now try `tnt fetch`, which tries to extract the strings and creates the language files in `.tnt/translation-[tag].json`. `tnt` will show what's being done and will print a status for each translation file that consists of its tag, its counters, the language name, and the filename of the translation file.

Of particular interest are the counters, these count the states the individual strings are in. If you extracted, say 5 strings and haven't translated them yet, you'll see here a `[5n]`. Later counters for additional states will appear. If you are interested now, skip to [TranslationCounters](#TranslationCounters) for more information.

### Translating Strings

Now `tnt` does not support (yet?) interactive translations with the command line. Of course you can change the translation files, but `tnt` helps you in two other ways. 

First, it supports the traditional translation roundrip that consists of exporting the translation files to the [XLIFF][XLIFF] format and importing them back. With `tnt export` XLIFF files can be generated and sent to human translators, who can then use their favorite tool (like the Multilingual App Toolkit, for example) to translate these files and send them back. `tnt import` will then take care of the reintegration into the matching translation files.

Second, and absolutely surprising for me, machine translations. I translated 33 strings of one of my apps from English to German, the results needed minimal changes only and also correctly placed .NET placeholders like `{0}`. I don't know if your experience will be the same, but with `tnt translate` you can try your luck with Google Cloud Translation API.

> To use Google Cloud Translation API, follow the steps 1. to 3. in [Quickstart](https://cloud.google.com/translate/docs/quickstart), and then invoke `tnt translate`, which will machine translate all your new strings.

### Deployment & Translation Loading

TBD

## Caveats

TBD

## Reference

### Command Line, Parameters, and Examples

#### `tnt init`

####`tnt add`

#### `tnt fetch`

#### `tnt gc`

#### `tnt status`

#### `tnt export`

#### `tnt import`

#### `tnt translate`

#### `tnt sync`

#### `tnt show`

#### `tnt help`

#### `tnt version`

### TranslationCounters

- **n**ew  
  Yet untranslated strings.

- needs **r**eview  
  Machine translated strings, or strings imported that need a review XLIFF.
- **f**inal  
  Strings imported that were marked "translated" or "final".
- **u**nused  
  Translated strings of which their original string was not found in the list of sources after a `tnt fetch`.

### Directory `.tnt` 

#### File `.tnt/sources.json` 

#### File `.tnt/translation-[tag].json` 

### Directory `.tnt-content` 

#### File `.tnt-content/[tag].tnt` 

## License



