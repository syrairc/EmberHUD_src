using System.Globalization;
using System.Reflection;
using EmberHUD.Host;

namespace EmberHUD.Tests;

public class OffsetsTests
{
    private static string[] WantLines() => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "layout.want")).Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToArray();

    private static Dictionary<string, string> WantEntries() =>
        WantLines()
            .TakeWhile(line => !line.StartsWith("[build"))
            .Select(line => line.Split('=', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[0], parts => parts[1]);

    private static Dictionary<long, Dictionary<string, long>> WantBuilds()
    {
        var builds = new Dictionary<long, Dictionary<string, long>>();
        Dictionary<string, long>? current = null;
        foreach (var line in WantLines().SkipWhile(line => !line.StartsWith("[build")).TakeWhile(line => line != "[sigs]"))
        {
            if (line.StartsWith("[build"))
            {
                current = builds[long.Parse(line[6..^1], CultureInfo.InvariantCulture)] = [];
                continue;
            }
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            current![parts[0]] = parts[1].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? long.Parse(parts[1][2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : long.Parse(parts[1], CultureInfo.InvariantCulture);
        }
        return builds;
    }

    private static IEnumerable<PropertyInfo> OffsetProperties<T>() =>
        typeof(Offsets).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Where(p => p.PropertyType == typeof(T));

    [Fact]
    public void EmbeddedTableDecryptsToEveryWantFileValue()
    {
        var want = WantEntries()
            .Where(entry => !entry.Value.StartsWith('"'))
            .ToDictionary(entry => entry.Key, entry => entry.Value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.Parse(entry.Value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : long.Parse(entry.Value, CultureInfo.InvariantCulture));

        var properties = OffsetProperties<int>().ToDictionary(p => p.Name, p => (long)(int)p.GetValue(null)!);
        var rvaNames = WantBuilds().Values.First().Keys;

        Assert.Equal(want.Count + rvaNames.Count, properties.Count);
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }

    [Fact]
    public void EveryBuildHasEveryRvaValueFromTheWantFile()
    {
        var builds = WantBuilds();
        Assert.Equal(builds.Keys.Order(), Offsets.BuildSizes.Order());
        var perBuildNames = builds.Values.First().Keys.ToHashSet();
        var properties = OffsetProperties<int>().Where(p => perBuildNames.Contains(p.Name)).ToDictionary(p => p.Name);
        var original = Offsets.Build;
        try
        {
            foreach (var (size, values) in builds)
            {
                Assert.True(Offsets.SelectBuild(size));
                Assert.Equal(properties.Keys.Order(), values.Keys.Order());
                foreach (var (name, value) in values)
                    Assert.Equal(value, (long)(int)properties[name].GetValue(null)!);
            }
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void TheSteamBuildHasThePortedRvas()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(Offsets.SelectBuild(124465080));
            Assert.Equal(0x6A5F388, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x6A5DCA8, Offsets.FightMgrGlobalRva);
            Assert.Equal(0x6A5FC70, Offsets.PlayerMgrGlobalRva);
            Assert.Equal(0x6A5FC48, Offsets.MiniMapMgrGlobalRva);
            Assert.Equal(0x6A60588, Offsets.WoodStakeMgrGlobalRva);
            Assert.Equal(0x8101AE8, Offsets.GObjectsGlobalRva);
            Assert.Equal(0x80E8E40, Offsets.FNamePoolRva);
            Assert.Equal(0x8248A90, Offsets.GEngineGlobalRva);
            Assert.Equal(0x782ADA0, Offsets.MonsterCfgBucketsRva);
            Assert.Equal(0x782ADAC, Offsets.MonsterCfgBucketCountRva);
            Assert.Equal(0x779A8BA, Offsets.LanguageRva);
            Assert.Equal(0x77DABA0, Offsets.LanguageBucketsRva);
            Assert.Equal(0x77DABAC, Offsets.LanguageBucketCountRva);
            Assert.Equal(0x6F51C80, Offsets.MapIconInfoBucketsRva);
            Assert.Equal(0x6F51C8C, Offsets.MapIconInfoBucketCountRva);

            Assert.True(Offsets.SelectBuild(123144120));
            Assert.Equal(0x69331B8, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x7FBCC80, Offsets.FNamePoolRva);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void TheEighteenthBuildHasItsOwnRvas()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(Offsets.SelectBuild(125921720));
            Assert.Equal(0x6BAFA78, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x6BB0CD0, Offsets.WoodStakeMgrGlobalRva);
            Assert.Equal(0x8147E00, Offsets.LuaGlobalRva);
            Assert.Equal(0x710, Offsets.FightMapNpc);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void TheSeventeenthBuildHasItsOwnRvasAndMovedStructOffsets()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(Offsets.SelectBuild(126258104));
            Assert.Equal(0x6BFC1A8, Offsets.LogicWorldGlobalRva);
            Assert.Equal(0x6BFD400, Offsets.WoodStakeMgrGlobalRva);
            Assert.Equal(0x710, Offsets.FightMapNpc);
            Assert.Equal(0x8A0, Offsets.FightMapServant);
            Assert.Equal(142, Offsets.EntityHasPosition);
            Assert.Equal(936, Offsets.EntityPosition);
            Assert.Equal(944, Offsets.EntityHeaderSize);
            Assert.Equal(768, Offsets.EntityRole);
            Assert.Equal(376, Offsets.WoodStakeStatistic);
            Assert.Equal(224, Offsets.WoodStakeUnlimitedTotal);
            Assert.Equal(0xD0, Offsets.FightMapPlayer);

            Assert.True(Offsets.SelectBuild(123144120));
            Assert.Equal(0x6C0, Offsets.FightMapNpc);
            Assert.Equal(920, Offsets.EntityPosition);
            Assert.True(Offsets.SelectBuild(124465080));
            Assert.Equal(0x6C0, Offsets.FightMapNpc);
            Assert.Equal(752, Offsets.EntityRole);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Theory]
    [InlineData(123144120, true, 123144120)]
    [InlineData(124465080, true, 124465080)]
    [InlineData(125921720, true, 125921720)]
    [InlineData(126258104, true, 126258104)]
    [InlineData(127223224, true, 127223224)]
    [InlineData(123804599, false, 123144120)]
    [InlineData(123804600, false, 124465080)]
    [InlineData(124000000, false, 124465080)]
    [InlineData(125193399, false, 124465080)]
    [InlineData(125193400, false, 125921720)]
    [InlineData(126089912, false, 125921720)]
    [InlineData(126089913, false, 126258104)]
    [InlineData(126740663, false, 126258104)]
    [InlineData(126740664, false, 127223224)]
    [InlineData(100, false, 123144120)]
    [InlineData(999999999, false, 127223224)]
    public void SelectBuildPicksExactThenClosestThenLater(long exeSize, bool exact, long expectedBuild)
    {
        var original = Offsets.Build;
        try
        {
            Assert.Equal(exact, Offsets.SelectBuild(exeSize));
            Assert.Equal(expectedBuild, Offsets.BuildSizes[Offsets.Build]);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void LuaLayoutKeysExist()
    {
        Assert.Equal(24, Offsets.LuaMgrState);
        Assert.Equal(24, Offsets.LuaStateGlobal);
        Assert.Equal(64, Offsets.LuaGlobalRegistry);
        Assert.Equal(80, Offsets.LuaGlobalSeed);
        Assert.Equal(2, Offsets.LuaRegistryGlobals);
        Assert.Equal(11, Offsets.LuaTableLogSize);
        Assert.Equal(12, Offsets.LuaTableArraySize);
        Assert.Equal(16, Offsets.LuaTableArray);
        Assert.Equal(24, Offsets.LuaTableNode);
        Assert.Equal(40, Offsets.LuaTableMetatable);
        Assert.Equal(16, Offsets.LuaTValueSize);
        Assert.Equal(32, Offsets.LuaNodeSize);
        Assert.Equal(0, Offsets.LuaNodeValue);
        Assert.Equal(8, Offsets.LuaNodeTag);
        Assert.Equal(16, Offsets.LuaNodeKey);
        Assert.Equal(24, Offsets.LuaNodeKeyTag);
        Assert.Equal(12, Offsets.LuaStringHash);
        Assert.Equal(16, Offsets.LuaStringLength);
        Assert.Equal(32, Offsets.LuaStringChars);
        Assert.Equal(0x13, Offsets.LuaTagInt);
        Assert.Equal(0x44, Offsets.LuaTagShortString);
        Assert.Equal(0x54, Offsets.LuaTagLongString);
        Assert.Equal(0x45, Offsets.LuaTagTable);
        Assert.Equal(0x03, Offsets.LuaTagFloat);
        Assert.Equal(0x01, Offsets.LuaTagBool);
        Assert.Equal(0x46, Offsets.LuaTagFunction);
        Assert.Equal(12, Offsets.MaxLuaLogSize);
        Assert.Equal(65536, Offsets.MaxLuaArray);
        Assert.Equal(256, Offsets.MaxLuaString);
        Assert.Equal(1024, Offsets.MaxLuaDumpEntries);
        Assert.Equal(100, Offsets.InventoryTabFirst);
        Assert.Equal(4, Offsets.InventoryTabCount);
        Assert.Equal(1000, Offsets.MaxInventoryItems);
        Assert.Equal(500, Offsets.InventoryRefreshMilliseconds);
        Assert.Equal(3, Offsets.InventoryWantedSeconds);
        Assert.Equal(100, Offsets.CharacterRefreshMilliseconds);
        Assert.Equal(3, Offsets.CharacterWantedSeconds);
        Assert.Equal(120, Offsets.FightWorldFirst);
        Assert.Equal(56, Offsets.FightWorldSecond);
        Assert.Equal(48, Offsets.FightWorldThird);
        Assert.Equal(24, Offsets.FightWorldFightMsg);
        Assert.Equal(0x100, Offsets.FightMsgFrame);
        Assert.Equal(200, Offsets.PausedAfterMilliseconds);
        Assert.Equal(300, Offsets.ChatHoldMilliseconds);
        Assert.Equal("ChatV3_C", Offsets.ChatPageClassName);
        Assert.Equal(512, Offsets.FightWorldMap);
        Assert.Equal(524, Offsets.FightWorldMapCount);
        Assert.Equal(8, Offsets.FightNodeValue);
        Assert.Equal(16, Offsets.FightNodeKey);
        Assert.Equal(96, Offsets.FightEntityComponentIndex);
        Assert.Equal(104, Offsets.FightEntityComponentSlots);
        Assert.Equal(18, Offsets.ComponentBuff);
        Assert.Equal(216, Offsets.BuffMap);
        Assert.Equal(228, Offsets.BuffMapCount);
        Assert.Equal(192, Offsets.BuffTotal);
        Assert.Equal(200, Offsets.BuffRemaining);
        Assert.Equal(208, Offsets.BuffCfgRow);
        Assert.Equal(488, Offsets.BuffId);
        Assert.Equal(12, Offsets.BuffCfgId);
        Assert.Equal("char_buff|name|", Offsets.BuffNameKey);
        Assert.Equal("_0", Offsets.BuffNameSuffix);
        Assert.Equal(128, Offsets.MaxBuffs);
        Assert.Equal(130, Offsets.ComponentSkill);
        Assert.Equal(248, Offsets.CooldownMap);
        Assert.Equal(260, Offsets.CooldownMapCount);
        Assert.Equal(88, Offsets.CooldownNodeSize);
        Assert.Equal(80, Offsets.CooldownNodeNext);
        Assert.Equal(0, Offsets.CooldownSkill);
        Assert.Equal(8, Offsets.CooldownCharges);
        Assert.Equal(12, Offsets.CooldownMaxCharges);
        Assert.Equal(16, Offsets.CooldownRemaining);
        Assert.Equal(24, Offsets.CooldownTotal);
        Assert.Equal(131, Offsets.ComponentSkillCfg);
        Assert.Equal(64, Offsets.SkillCfgPointer);
        Assert.Equal("CtrlPad_C", Offsets.PadPageClassName);
        Assert.Equal(0x3C8, Offsets.SkillCfgIdsOffset);
        Assert.Equal(64, Offsets.MaxSkills);
        Assert.Equal("ItemMgr", Offsets.LuaItemManager);
        Assert.Equal("ItemList", Offsets.LuaItemBags);
        Assert.Equal("BagMgr", Offsets.LuaBagManager);
        Assert.Equal("BagCapacity", Offsets.LuaBagCapacity);
        Assert.Equal("_Raw_Data", Offsets.LuaItemRecord);
        Assert.Equal("ItemId", Offsets.LuaItemId);
        Assert.Equal("ConfigBaseId", Offsets.LuaItemBaseId);
        Assert.Equal("Level", Offsets.LuaItemLevel);
        Assert.Equal("Quality", Offsets.LuaItemQuality);
        Assert.Equal("RareValue", Offsets.LuaItemRareValue);
        Assert.Equal("ConfigSpecialId", Offsets.LuaItemSpecialId);
        Assert.Equal("BagNum", Offsets.LuaItemCount);
        Assert.Equal("MaxNum", Offsets.LuaItemStackMax);
        Assert.Equal("Type1", Offsets.LuaItemType);
        Assert.Equal("AffixList", Offsets.LuaItemAffixList);
        Assert.Equal("PrefixInfoList", Offsets.LuaItemPrefixInfoList);
        Assert.Equal("SuffixInfoList", Offsets.LuaItemSuffixInfoList);
    }

    [Fact]
    public void TartarusKeysExist()
    {
        Assert.Equal("S16GameplayMgr", Offsets.LuaTartarusManager);
        Assert.Equal("Board", Offsets.LuaTartarusBoard);
        Assert.Equal("Cells", Offsets.LuaTartarusCells);
        Assert.Equal("Position", Offsets.LuaTartarusPosition);
        Assert.Equal("Size", Offsets.LuaTartarusSize);
        Assert.Equal("MaxTurn", Offsets.LuaTartarusMaxTurn);
        Assert.Equal("Attrs", Offsets.LuaTartarusAttrs);
        Assert.Equal("Choices", Offsets.LuaTartarusChoices);
        Assert.Equal("Relics", Offsets.LuaTartarusRelics);
        Assert.Equal("Archive", Offsets.LuaTartarusArchive);
        Assert.Equal("Result", Offsets.LuaTartarusResult);
        Assert.Equal("BoardPageOpen", Offsets.LuaTartarusBoardPageOpen);
        Assert.Equal("prisoner", Offsets.LuaTartarusPrisoner);
        Assert.Equal("species", Offsets.LuaTartarusSpecies);
        Assert.Equal("rating", Offsets.LuaTartarusRating);
        Assert.Equal("uid", Offsets.LuaTartarusUid);
        Assert.Equal("isDevourTarget", Offsets.LuaTartarusDevourTarget);
        Assert.Equal("hasEventMarker", Offsets.LuaTartarusEventMarker);
        Assert.Equal("choiceType", Offsets.LuaTartarusChoiceType);
        Assert.Equal("choiceId", Offsets.LuaTartarusChoiceId);
        Assert.Equal("options", Offsets.LuaTartarusOptions);
        Assert.Equal("optionId", Offsets.LuaTartarusOptionId);
        Assert.Equal("dice", Offsets.LuaTartarusDice);
        Assert.Equal("step", Offsets.LuaTartarusStep);
        Assert.Equal("operation", Offsets.LuaTartarusOperation);
        Assert.Equal("operationType", Offsets.LuaTartarusOperationType);
        Assert.Equal("devour", Offsets.LuaTartarusDevour);
        Assert.Equal("targetCellIndex", Offsets.LuaTartarusTarget);
        Assert.Equal("interrogation", Offsets.LuaTartarusInterrogation);
        Assert.Equal("optionType", Offsets.LuaTartarusInterrogationType);
        Assert.Equal("relic", Offsets.LuaTartarusRelic);
        Assert.Equal("relicId", Offsets.LuaTartarusRelicId);
        Assert.Equal("obtainSequence", Offsets.LuaTartarusObtainSequence);
        Assert.Equal("coloredReward", Offsets.LuaTartarusColoredReward);
        Assert.Equal("rewardId", Offsets.LuaTartarusRewardId);
        Assert.Equal("event", Offsets.LuaTartarusEvent);
        Assert.Equal("eventId", Offsets.LuaTartarusEventId);
        Assert.Equal(32, Offsets.TartarusDayAttr);
        Assert.Equal(201, Offsets.TartarusRatingBase);
        Assert.Equal(101, Offsets.TartarusSpeciesBase);
        Assert.Equal(1, Offsets.TartarusChoiceDice);
        Assert.Equal(3, Offsets.TartarusChoiceReward);
        Assert.Equal(4, Offsets.TartarusChoiceOffice);
        Assert.Equal(5, Offsets.TartarusChoiceEvent);
        Assert.Equal(8, Offsets.TartarusChoiceInstrument);
        Assert.Equal(9, Offsets.TartarusChoiceOperation);
        Assert.Equal(1, Offsets.TartarusOpDevour);
        Assert.Equal(2, Offsets.TartarusOpReplace);
        Assert.Equal(3, Offsets.TartarusOpEnd);
        Assert.Equal(4, Offsets.TartarusOpStopChain);
        Assert.Equal(1, Offsets.TartarusInstigate);
        Assert.Equal(2, Offsets.TartarusBestow);
        Assert.Equal(100, Offsets.TartarusRefreshMilliseconds);
        Assert.Equal(3, Offsets.TartarusWantedSeconds);
        Assert.Equal(64, Offsets.MaxTartarusCells);
        Assert.Equal(16, Offsets.MaxTartarusOptions);
        Assert.Equal(16, Offsets.MaxTartarusRelics);
    }

    [Fact]
    public void TarotKeysExist()
    {
        Assert.Equal("S9GamePlayMgr", Offsets.LuaTarotManager);
        Assert.Equal("PlayRound", Offsets.LuaTarotRound);
        Assert.Equal("S9Data", Offsets.LuaTarotData);
        Assert.Equal("State", Offsets.LuaTarotState);
        Assert.Equal("StateData", Offsets.LuaTarotStateData);
        Assert.Equal("CardPlay", Offsets.LuaTarotCardPlay);
        Assert.Equal("HP", Offsets.LuaTarotHp);
        Assert.Equal("TileMap", Offsets.LuaTarotTileMap);
        Assert.Equal("Tiles", Offsets.LuaTarotTiles);
        Assert.Equal("CurrentInx", Offsets.LuaTarotPosition);
        Assert.Equal("CfgID", Offsets.LuaTarotCfg);
        Assert.Equal("ID", Offsets.LuaTarotId);
        Assert.Equal("S9Reward", Offsets.LuaTarotReward);
        Assert.Equal("CardGroup", Offsets.LuaTarotCardGroup);
        Assert.Equal("Cards", Offsets.LuaTarotCards);
        Assert.Equal("RecvRewards", Offsets.LuaTarotRecvRewards);
        Assert.Equal("playNum", Offsets.LuaTarotPlayNum);
        Assert.Equal("ChallengeTimes", Offsets.LuaTarotChallengeTimes);
        Assert.Equal("S9GamePlayStateCardCollect", Offsets.TarotStateCollect);
        Assert.Equal("S9GamePlayStateCardPlay", Offsets.TarotStateBoard);
        Assert.Equal("S9GamePlayStateFinalPlay", Offsets.TarotStateFinal);
        Assert.Equal(1, Offsets.TarotProtoTileMapTiles);
        Assert.Equal(2, Offsets.TarotProtoTileMapPosition);
        Assert.Equal(2, Offsets.TarotLazyBytesKey);
        Assert.Equal(2, Offsets.TarotProtoTileCfg);
        Assert.Equal(4, Offsets.TarotProtoTileReward);
        Assert.Equal(2, Offsets.TarotProtoRewardCfg);
        Assert.Equal(1, Offsets.TarotProtoGroupCards);
        Assert.Equal(1, Offsets.TarotProtoCardId);
        Assert.Equal(2, Offsets.TarotProtoCardCfg);
        Assert.Equal(7, Offsets.TarotChestRarities);
        Assert.Equal(100, Offsets.TarotRefreshMilliseconds);
        Assert.Equal(3, Offsets.TarotWantedSeconds);
        Assert.Equal(256, Offsets.MaxTarotTiles);
        Assert.Equal(32, Offsets.MaxTarotCards);
        Assert.Equal(32, Offsets.MaxTarotRewards);
        Assert.Equal(65536, Offsets.MaxTarotBlob);
    }

    [Fact]
    public void VoraxKeysExist()
    {
        Assert.Equal("ViewMgr", Offsets.LuaVoraxViewManager);
        Assert.Equal("SingleViewList", Offsets.LuaVoraxViewList);
        Assert.Equal("S13GamePlayMainCtrl", Offsets.LuaVoraxPageSuffix);
        Assert.Equal("ViewData", Offsets.LuaVoraxViewData);
        Assert.Equal("BaseAttr", Offsets.LuaVoraxBaseAttr);
        Assert.Equal("S13GamePlayMgr", Offsets.LuaVoraxManager);
        Assert.Equal("S13CardPlay_NormalRound", Offsets.VoraxStateRound);
        Assert.Equal(4, Offsets.VoraxAttrRound);
        Assert.Equal(2, Offsets.VoraxAttrRounds);
        Assert.Equal(1, Offsets.VoraxAttrRerolls);
        Assert.Equal(3, Offsets.VoraxAttrSlots);
        Assert.Equal(102, Offsets.VoraxTypePotion);
        Assert.Equal("TextTable_GameFunc|value|", Offsets.VoraxTypeNamePrefix);
        Assert.Equal(100, Offsets.VoraxRefreshMilliseconds);
        Assert.Equal(3, Offsets.VoraxWantedSeconds);
        Assert.Equal("map_gameplay_season_plague_card_monster|name|", Offsets.VoraxMonsterNameKey);
    }

    [Fact]
    public void GroundItemKeysExist()
    {
        Assert.Equal("GroundMgr", Offsets.LuaGroundManager);
        Assert.Equal("GroundItems", Offsets.LuaGroundItems);
        Assert.Equal("DropItem", Offsets.LuaGroundDropItem);
        Assert.Equal("belongState", Offsets.LuaGroundBelong);
        Assert.Equal("DropId", Offsets.LuaGroundDropId);
        Assert.Equal("item", Offsets.LuaGroundItem);
        Assert.Equal("Prefix", Offsets.LuaGroundPrefix);
        Assert.Equal("Suffix", Offsets.LuaGroundSuffix);
        Assert.Equal("ConfigSpecialId", Offsets.LuaGroundSpecialId);
        Assert.Equal("UniqueItmId", Offsets.LuaGroundUniqueId);
        Assert.Equal("BaseId", Offsets.LuaGroundBaseId);
        Assert.Equal("Num", Offsets.LuaGroundCount);
        Assert.Equal("RareValue", Offsets.LuaGroundRareValue);
        Assert.Equal("Level", Offsets.LuaGroundLevel);
        Assert.Equal("Id", Offsets.LuaGroundAffixId);
        Assert.Equal(100, Offsets.GroundItemsRefreshMilliseconds);
        Assert.Equal(3, Offsets.GroundItemsWantedSeconds);
        Assert.Equal(200, Offsets.MaxGroundItems);
        Assert.Equal(1, Offsets.GroundCommodityMaxLevel);
        Assert.Equal(12, Offsets.MaxGroundAffixes);
    }

    [Fact]
    public void NameTagKeysExist()
    {
        Assert.Equal("DropMgr", Offsets.DropMgrClassName);
        Assert.Equal(0x3E8, Offsets.DropMgrNameTagGroups);
        Assert.Equal(144, Offsets.NameTagGroupStride);
        Assert.Equal(8, Offsets.NameTagGroupValue);
        Assert.Equal(0, Offsets.NameTagGroupWorldPos);
        Assert.Equal(0x10, Offsets.NameTagGroupScreenRects);
        Assert.Equal(0x74, Offsets.NameTagGroupInScreen);
        Assert.Equal(0x78, Offsets.NameTagGroupScreenPos);
        Assert.Equal(32, Offsets.ScreenRectStride);
        Assert.Equal(4, Offsets.ScreenRectBox);
        Assert.Equal(128, Offsets.MaxNameTagGroups);
        Assert.Equal(64, Offsets.MaxScreenRects);
        Assert.Equal(1000, Offsets.MaxLabelPixels);
        Assert.Equal(5, Offsets.DropMgrRescanSeconds);
    }

    [Fact]
    public void LuaGlobalRvaIsSetForTheCurrentBuild()
    {
        var original = Offsets.Build;
        try
        {
            Assert.True(Offsets.SelectBuild(126258104));
            Assert.NotEqual(0, Offsets.LuaGlobalRva);
        }
        finally
        {
            Offsets.Build = original;
        }
    }

    [Fact]
    public void NonAsciiStringsSurviveTheSealedTable()
    {
        Assert.Equal("实体ID", Offsets.TarotEntityVar);
    }

    [Fact]
    public void TheHpBarKeysAreTheProbedValues()
    {
        Assert.Equal("ERoleUIComponent", Offsets.HpBarComponentClassName);
        Assert.Equal(0xC0, Offsets.ComponentEntity);
        Assert.Equal(0x128, Offsets.HpBarWidgetWeak);
        Assert.Equal("HpBarMonster_C", Offsets.HpBarNormalClassName);
        Assert.Equal("HpBarMonsterElite_C", Offsets.HpBarEliteClassName);
        Assert.Equal(0x298, Offsets.HpBarNormalCanvas);
        Assert.Equal(0x2B0, Offsets.HpBarEliteCanvas);
        Assert.Equal(0x308, Offsets.HpBarNormalTrack);
        Assert.Equal(0x2E0, Offsets.HpBarEliteTrack);
        Assert.Equal("HpBarMonsterBoss_C", Offsets.HpBarBossClassName);
        Assert.Equal(0x298, Offsets.HpBarBossCanvas);
        Assert.Equal(0x330, Offsets.HpBarBossTrack);
    }

    [Fact]
    public void NpcNameKeyIsTheNpcNameLocPrefix()
    {
        Assert.Equal("npc|name|", Offsets.NpcNameKey);
    }

    [Fact]
    public void EmbeddedTableDecryptsToEveryWantFileString()
    {
        var want = WantEntries().Where(entry => entry.Value.StartsWith('"')).ToDictionary(entry => entry.Key, entry => entry.Value.Trim('"'));
        var properties = OffsetProperties<string>().ToDictionary(p => p.Name, p => (string)p.GetValue(null)!);

        Assert.Equal(want.Count, properties.Count);
        foreach (var (name, value) in want)
            Assert.Equal(value, properties[name]);
    }

    [Fact]
    public void TheTMapHeaderSizeCoversItsFields()
    {
        Assert.True(Offsets.TMapHeaderSize >= nint.Size);
        Assert.True(Offsets.TMapHeaderSize >= Offsets.TArrayNum + sizeof(int));
        Assert.True(Offsets.TMapHeaderSize >= Offsets.TMapInlineBits + Offsets.TMapInlineBitCount / 8);
        Assert.True(Offsets.TMapHeaderSize >= Offsets.TMapSecondaryBits + nint.Size);
    }

    [Fact]
    public void TheLocAndVarNodeSizesCoverTheirFields()
    {
        Assert.True(Offsets.LocNodeSize >= Offsets.LocNodeValue + nint.Size);
        Assert.True(Offsets.LocNodeSize >= Offsets.LocNodeNext + nint.Size);
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeName + nint.Size);
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeNameLength + sizeof(int));
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeValue + sizeof(int));
        Assert.True(Offsets.VarNodeSize >= Offsets.VarNodeNext + nint.Size);
    }

    [Fact]
    public void TheCameraPovSizeCoversItsFields()
    {
        Assert.True(Offsets.CameraPovSize >= Offsets.PovLocation + 3 * sizeof(float));
        Assert.True(Offsets.CameraPovSize >= Offsets.PovRotation + 3 * sizeof(float));
        Assert.True(Offsets.CameraPovSize >= Offsets.PovFov + sizeof(float));
    }

    [Fact]
    public void TheGeometryAndHybridHeaderSizesCoverTheirFields()
    {
        Assert.True(Offsets.GeometryHeaderSize >= Offsets.GeometryCellSize + sizeof(int));
        Assert.True(Offsets.GeometryHeaderSize >= Offsets.GeometryOriginX + sizeof(double));
        Assert.True(Offsets.GeometryHeaderSize >= Offsets.GeometryOriginY + sizeof(double));
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridCols + sizeof(int));
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridRows + sizeof(int));
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridCells + nint.Size);
        Assert.True(Offsets.HybridHeaderSize >= Offsets.HybridIsHybrid + sizeof(byte));
    }

    [Fact]
    public void ParseSigLinesReadsBothKinds()
    {
        var (sigs, relations) = Offsets.ParseSigLines(["S|A|48 8B ?|3|7", "R|A|B|-12"]);
        Assert.Equal([new RvaSig("A", "48 8B ?", 3, 7)], sigs);
        Assert.Equal([new RvaRelation("A", "B", -12)], relations);
    }

    [Fact]
    public void SetPerBuildChangesOnlyTheCurrentBuild()
    {
        var snapshot = Offsets.Snapshot();
        try
        {
            Offsets.SelectBuild(126258104);
            Assert.True(Offsets.SetPerBuild("LogicWorldGlobalRva", 0x1234));
            Assert.Equal(0x1234, Offsets.LogicWorldGlobalRva);
            Assert.True(Offsets.TryGetPerBuild("LogicWorldGlobalRva", out var value));
            Assert.Equal(0x1234, value);
            Offsets.SelectBuild(123144120);
            Assert.Equal(0x69331B8, Offsets.LogicWorldGlobalRva);
        }
        finally
        {
            Offsets.Restore(snapshot);
        }
    }

    [Fact]
    public void SetPerBuildRejectsUnknownKey()
    {
        Assert.False(Offsets.SetPerBuild("NoSuchKey", 1));
        Assert.False(Offsets.TryGetPerBuild("NoSuchKey", out _));
    }

    [Fact]
    public void TheShippedSigsParse()
    {
        Assert.NotEmpty(Offsets.Sigs);
        Assert.All(Offsets.Sigs, sig => Assert.Contains(sig.Key, Offsets.BuildKeys));
        Assert.All(Offsets.Relations, relation => Assert.Contains(relation.Key, Offsets.BuildKeys));
    }
}
