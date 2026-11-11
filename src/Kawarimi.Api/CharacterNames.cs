namespace Kawarimi;

public static class CharacterNames
{
    static readonly Dictionary<string, string> ByFamily = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nrt"] = "Naruto",
        ["ssk"] = "Sasuke",
        ["skr"] = "Sakura",
        ["roc"] = "Rock Lee",
        ["nej"] = "Neji",
        ["ten"] = "Tenten",
        ["ino"] = "Ino",
        ["sik"] = "Shikamaru",
        ["tyo"] = "Choji",
        ["kib"] = "Kiba",
        ["sin"] = "Shino",
        ["hnt"] = "Hinata",
        ["gar"] = "Gaara",
        ["knk"] = "Kankuro",
        ["tmr"] = "Temari",
        ["kks"] = "Kakashi",
        ["guy"] = "Might Guy",
        ["asm"] = "Asuma",
        ["jry"] = "Jiraiya",
        ["tnd"] = "Tsunade",
        ["orc"] = "Orochimaru",
        ["kbt"] = "Kabuto",
        ["itc"] = "Itachi",
        ["ksm"] = "Kisame",
        ["cyb"] = "Chiyo",
        ["sai"] = "Sai",
        ["ymt"] = "Yamato",
        ["sco"] = "Sasori",
        ["ddr"] = "Deidara",
        ["hdn"] = "Hidan",
        ["kzu"] = "Kakuzu",
        ["sgt"] = "Suigetsu",
        ["jug"] = "Jugo",
        ["kar"] = "Karin",
        ["tob"] = "Tobi",
        ["knn"] = "Konan",
        ["pea"] = "Pain",
        ["klb"] = "Killer Bee",
        ["mdr"] = "Madara",
        ["obt"] = "Obito",
        ["hak"] = "Haku",
        ["zbz"] = "Zabuza",
        ["ngt"] = "Nagato",
        ["kgy"] = "Kaguya",
    };

    public static IReadOnlyDictionary<string, string> Families => ByFamily;

    public static string? For(string characterCode) =>
        characterCode.Length == 4 && ByFamily.TryGetValue(characterCode[1..], out var name) ? name : null;
}
