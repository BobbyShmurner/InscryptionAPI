using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using System.Collections.ObjectModel;
using InscryptionAPI.Helpers;
using InscryptionAPI.Card;

namespace InscryptionAPI.Ascension;

[HarmonyPatch]
public static class StarterDeckManager
{

    public class FullStarterDeck
    {
        public StarterDeckInfo Info { get; set; }
        public int UnlockLevel { get; set; }
        public List<string> CardNames { get; set; } // For delayed loading of actual card names
    }

    public static readonly ReadOnlyCollection<FullStarterDeck> BaseGameDecks = new(GetBaseGameDecks());

    internal static readonly ObservableCollection<FullStarterDeck> NewDecks = new();

    public static List<FullStarterDeck> AllDecks = BaseGameDecks.ToList();
    public static List<StarterDeckInfo> AllDeckInfos = BaseGameDecks.Select(fsd => fsd.Info).ToList();

    private static List<FullStarterDeck> GetBaseGameDecks()
    {
        List<StarterDeckInfo> decks = new(Resources.LoadAll<StarterDeckInfo>("Data/Ascension/StarterDecks"));
        InscryptionAPIPlugin.Logger.LogDebug($"Known starter decks: {String.Join(",", decks.Select(asci => asci.name))}");

        GameObject screen = Resources.Load<GameObject>("prefabs/ui/ascension/AscensionStarterDeckScreen");
        Transform iconContainer = screen.transform.Find("Icons");
        List<FullStarterDeck> retval = new ();
        for (int i = 1; i <= 8; i++)
        {
            AscensionStarterDeckIcon icon = iconContainer.Find($"StarterDeckIcon_{i}").gameObject.GetComponent<AscensionStarterDeckIcon>();
            InscryptionAPIPlugin.Logger.LogDebug($"Looking for deck named {icon.Info.name}");

            FullStarterDeck fdeck = new() { Info = decks.First(d => d.name == icon.Info.name), UnlockLevel = -1 };
            fdeck.CardNames = fdeck.Info.cards.Select(ci => ci.name).ToList();
            retval.Add(fdeck);
        }
        return retval;
    }

    private static FullStarterDeck CloneStarterDeck(FullStarterDeck info)
    {
        FullStarterDeck retval = new();
        StarterDeckInfo deckInfo = ScriptableObject.CreateInstance<StarterDeckInfo>();
        deckInfo.cards = info.Info.cards == null ? null : new List<CardInfo>(info.Info.cards);
        deckInfo.iconSprite = info.Info.iconSprite;
        deckInfo.name = info.Info.name;
        deckInfo.title = info.Info.title;
        retval.Info = deckInfo;
        retval.CardNames = new(info.CardNames);
        retval.UnlockLevel = info.UnlockLevel;
        return retval;
    }

    public static event Func<List<FullStarterDeck>, List<FullStarterDeck>> ModifyDeckList;

    public static void SyncDeckList()
    {
        var decks = BaseGameDecks.Concat(NewDecks).Select(x => CloneStarterDeck(x)).ToList();       

        foreach (var deck in decks)
            deck.Info.cards = deck.CardNames.Select(n => CardManager.AllCardsCopy.CardByName(n)).ToList();

        AllDecks = ModifyDeckList?.Invoke(decks) ?? decks;
        AllDeckInfos = AllDecks.Select(fsd => fsd.Info).ToList();
    }

    static StarterDeckManager()
    {
        InscryptionAPIPlugin.ScriptableObjectLoaderLoad += static type =>
        {
            if (type == typeof(StarterDeckInfo))
            {
                ScriptableObjectLoader<StarterDeckInfo>.allData = AllDecks.Select(fsd => fsd.Info).ToList();
            }
        };
        NewDecks.CollectionChanged += static (_, _) =>
        {
            SyncDeckList();
        };
    }

    public static FullStarterDeck Add(string pluginGuid, StarterDeckInfo info, int unlockLevel = 0)
    {
        info.name = pluginGuid + "_" + (info.title ?? info.name);

        FullStarterDeck fsd = new();
        fsd.Info = info;
        fsd.UnlockLevel  = unlockLevel;
        fsd.CardNames = info.cards.Select(ci => ci.name).ToList();
        
        if (NewDecks.FirstOrDefault(sdi => sdi.Info.name == info.name) == null)
            NewDecks.Add(fsd);

        return fsd;
    }

    public static FullStarterDeck New(string pluginGuid, string title, Texture2D iconTexture, string[] cardNames, int unlockLevel = 0)
    {
        StarterDeckInfo info = ScriptableObject.CreateInstance<StarterDeckInfo>();
        info.title = title;
        info.iconSprite = TextureHelper.ConvertTexture(iconTexture, TextureHelper.SpriteType.StarterDeckIcon);
        info.name = pluginGuid + "_" + title;
        
        FullStarterDeck fsd = new();
        fsd.Info = info;
        fsd.UnlockLevel = unlockLevel;
        fsd.CardNames = cardNames.ToList();

        NewDecks.Add(fsd);

        return fsd;
    }

    public static FullStarterDeck New(string pluginGuid, string title, string pathToIconTexture, string[] cardNames, int unlockLevel = 0)
    {
        Texture2D texture = TextureHelper.GetImageAsTexture(pathToIconTexture);
        return New(pluginGuid, title, texture, cardNames, unlockLevel);
    }

    [HarmonyPatch(typeof(AscensionUnlockSchedule), "StarterDeckIsUnlockedForLevel")]
    [HarmonyPrefix]
    public static bool CustomStarterLevel(ref bool __result, string id, int level)
    {
        FullStarterDeck fsd = AllDecks.FirstOrDefault(d => d.Info.name == id);
        if (fsd == null || fsd.UnlockLevel < 0)
            return true;

        __result = level >= fsd.UnlockLevel;
        return false;
    }  
}