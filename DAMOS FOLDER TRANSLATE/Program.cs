// See https://aka.ms/new-console-template for more information
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

string fileContent;
string translateTo = "en";
string instanceToTranslate = "";
string fileNameWithoutExtension = "";
List<string> wordWithError = new List<string>();

// ETA
long lastETAUpdate = 0;
int instancesCount = 0;
double lastProgressCounter = 0.0f;
double progressCounter = 0.0f;

int argsLength = Environment.GetCommandLineArgs().Length;
if (argsLength > 1) {
    string fileName = "";
    for (int i = 0; i < argsLength; i++) {
        string parameter = Environment.GetCommandLineArgs()[i].ToLower();
        switch (parameter) {
            case "-l": // Set languages for output
            case "-i":
                if (i + 1 < argsLength) {
                    string value = Environment.GetCommandLineArgs()[i + 1].ToLower();
                    switch (value) {
                        case "fr":
                        case "es":
                        case "it":
                            translateTo = value;
                            break;
                        case "folders":
                            instanceToTranslate = "FUNCTION";
                            break;
                        case "maps":
                            instanceToTranslate = "CHARACTERISTIC";
                            break;
                        default:
                            break;
                    }
                }
                break;
            case "-h":
            case "-help":
                DisplayHelpMenu();
                return;
                break;
            default:
                // Check if it's the a2l file name
                string[] fileInfo = parameter.Split('.');
                if (fileInfo.Length > 1) { // File got an extension
                    if (fileInfo[1] == "a2l") {
                        fileName = parameter;
                        fileNameWithoutExtension = fileInfo[0].ToUpper();
                    }
                }
                break;
        }
    }

    string filePath = Directory.GetCurrentDirectory() + "\\" + fileName;

    try {
        using (StreamReader sr = new StreamReader(filePath, Encoding.GetEncoding("iso-8859-1"))) {
            fileContent = sr.ReadToEnd();
        }
    } catch (Exception) {
        Console.WriteLine("Couldn't find file.");
        return;
    }


    // Extracting instances data
    Console.WriteLine("Reading A2L datas...");
    List<string> blocks = new List<string>();
    foreach (Match match in Regex.Matches(fileContent, @"(?s)/begin " + instanceToTranslate + "(.+?)/end " + instanceToTranslate)) {
        blocks.Add(match.Groups[1].Value);
    }

    // Getting instances name from datas
    List<string> instancesNames = new List<string>();
    foreach (string b in blocks) {
        MatchCollection texts = Regex.Matches(b, "\"(.+?)\"");
        if (texts.Count > 0) {
            if (texts.First().Groups.Count > 1) {
                string instanceName = texts.First().Groups[1].Value;
                instancesNames.Add(instanceName);
            }
        }
    }

    int diffUnnamed = blocks.Count - instancesNames.Count;
    instancesCount = instancesNames.Count();
    Console.WriteLine(blocks.Count + " instances found. " + diffUnnamed + " of them are unnamed.");

    // Translating instances names
    TranslateInstances(instancesNames);

    // Check errors list
    int errorCounter = 0; // Once we reachead 5 tries, we gotta let it go
    while (wordWithError.Count > 0 && errorCounter++ < 5) {
        Console.BackgroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(wordWithError.Count + " errors has occured during the translation.");
        Console.ResetColor();
        Console.WriteLine("Trying to fix the errors...");
        List<string> instancesWithError = new List<string>(wordWithError);
        TranslateInstances(instancesWithError);
    }

    // Writing file
    string saveName = fileNameWithoutExtension + "_Translated_" + translateTo.ToUpper() + ".A2L";
    string savePath = Directory.GetCurrentDirectory() + "\\" + saveName;
    try {
        using (StreamWriter sw = new StreamWriter(savePath)) {
            sw.WriteLine(fileContent);
        }

    } catch (Exception) {
        Console.WriteLine("Unexpected error : couldn't write the translated file");
    }
    Console.BackgroundColor = ConsoleColor.DarkGreen;
    Console.WriteLine("Translation complete! File has been saved under the name:");
    Console.BackgroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine(saveName);
    Console.ResetColor();

    // PROGRAM END
} else {
    DisplayHelpMenu();
}

void DisplayHelpMenu() {
    Console.BackgroundColor = ConsoleColor.DarkGreen;
    Console.Write("[a2ltranslate] : Translate your a2l files from german into your desired output language ");
    Console.BackgroundColor = ConsoleColor.Blue;
    Console.WriteLine("(The07K.wiki)" + Environment.NewLine);
    Console.ResetColor();
    Console.WriteLine("Parameters:");
    Console.WriteLine("-l : Desired output language (en, fr, es, it) [Default: en]");
    Console.WriteLine("-i : Instances to translate (all, folders, maps) [Default: all]");
    Console.WriteLine("-h -help : Show this menu");
}

void TranslateInstances(List<string> instancesNames) {
    wordWithError.Clear();
    Console.Write("Translating the A2L in ");
    Console.BackgroundColor = ConsoleColor.Blue;
    Console.Write(GetNormalizedLanguage());
    Console.ResetColor();
    Console.WriteLine("....");

    using (ProgressBar progress = new ProgressBar()) {
        foreach (string untranslatedName in instancesNames) {
            string translatedWord = Translate(untranslatedName);
            if (!translatedWord.Equals("")) {
                fileContent = fileContent.Replace(untranslatedName, translatedWord);
            }
            progress.UpdateETA(CalculateETA());
            progress.Report(++progressCounter / Convert.ToDouble(instancesNames.Count()));
        }
    }
}

string Translate(string word) {
    var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=de&tl={translateTo}&dt=t&q={HttpUtility.UrlEncode(word)}";
#pragma warning disable SYSLIB0014
    string result = "";
    try {
        using (WebClient wc = new WebClient()) {
            wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 " +
                                          "(KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
            result = wc.DownloadString(url);
            result = result.Substring(4, result.IndexOf("\"", 4, StringComparison.Ordinal) - 4);
        }
    } catch (Exception) {
        wordWithError.Add(word); // We will try to re-translate it later
    }
    return result;
}

string GetNormalizedLanguage() {
    string nl = "";
    switch (translateTo) {
        case "fr":
            nl = "Français";
            break;
        case "en":
            nl = "English";
            break;
        case "es":
            nl = "Espagnol";
            break;
        case "it":
            nl = "Italiano";
            break;
        default:
            break;
    }
    return nl;
}

long CalculateETA() {
    int etaMS = 62; // Takes about 62ms per file in average
    double timeEllapsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastETAUpdate;
    if (timeEllapsed > 1000) {
        if (lastETAUpdate != 0) { // Updating ETA instead of using predefined one
            double wordsTranslated = progressCounter - lastProgressCounter;
            etaMS = (int)(wordsTranslated / timeEllapsed);
        }

        lastETAUpdate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        lastProgressCounter = progressCounter;
    }

    return  etaMS * (long)(instancesCount - progressCounter);
}


