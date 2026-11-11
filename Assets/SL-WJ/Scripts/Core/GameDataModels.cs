using System;
using System.Collections.Generic;

#region Server Communication Models — Init

[Serializable]
public class InitData
{
    public string id = "initData";
    public ServerGameData gameData;
    public ServerFeatures features;
    public ServerUIData uiData;
    public ServerPlayer player;
    // Wild Jokers sends no jackpot block. Kept because the platform's separate "jackpot:sync"
    // event reuses these types, and the init-time read is already null-guarded.
    public JackpotData jackpotData;
}

[Serializable]
public class JackpotData
{
    public JackpotValues values;
}

[Serializable]
public class JackpotValues
{
    public string miniJackpot;
    public string minorJackpot;
    public string majorJackpot;
    public string grandJackpot;
}

[Serializable]
public class JackpotSyncData
{
    public string gameId;
    public JackpotValues values;
}

// The four platform jackpot tiers, in the order the platform ranks them.
public enum JackpotTier
{
    Grand,
    Major,
    Minor,
    Mini
}

// Clicking a jackpot tier asks the platform to open its own jackpot overlay. Fire-and-forget —
// the server sends no direct reply; any resulting value change arrives on the usual jackpot:sync.
[Serializable]
public class JackpotOpenRequest
{
    public string type = "JACKPOT_OPEN";
    public JackpotOpenPayload payload = new JackpotOpenPayload();
}

[Serializable]
public class JackpotOpenPayload
{
    public string tier;
}

[Serializable]
public class ServerGameData
{
    // The 20 paylines, each one row index per reel, left to right, row 0 at the top. Confirmed
    // against live wins: a win's matchedPositions trace lines[lineIndex] cell for cell. Also read
    // for the reel count, which falls back to 5.
    public List<List<int>> lines;
    public List<double> bets;
    // The number of paylines (20), and the bet multiplier: every line is always played, so total bet
    // = the selected bet x this. Confirmed on every captured spin: at bet 0.01 the result reports a
    // totalBetAmount of 0.2 and the balance drops by exactly that. There is no creditDivisor or
    // activeLine in this init.
    public int totalLines;
}

[Serializable]
public class ServerFeatures
{
    public ServerWildFeature wild;
    public ServerFreeSpinsFeature freeSpins;
    public ServerPickBonusFeature ballPickemBonus;
    public ServerPickBonusFeature sandbellPickBonus;
}

// Every feature names its symbol. The ids are nullable so a missing one reads as "not sent" rather
// than 0, which is a real symbol (the Wild).
[Serializable]
public class ServerWildFeature
{
    public int? symbolId;
    // "enabled" is sent and deliberately unbound: the Wild has no round to switch off.
}

[Serializable]
public class ServerFreeSpinsFeature
{
    public bool enabled;
    public int? symbolId;          // the Drum
    public int spinsCount;         // spins awarded per trigger: 10
    // Zero-based: one Drum on each of these reels triggers. [1,2,3] = reels 2, 3 and 4.
    public List<int> triggerReels;
    // Zero-based. A Drum on each of these reels is the near miss that earns a respin of respinReel,
    // and what the client's anticipation watches. The backend currently counts Drums instead of
    // checking reels, here and in the trigger — reported, being fixed (BonusFreeSpins.md §9.5).
    public List<int> nearMissReels;
    public int respinReel;
}

// Ball Pick'em and Sandbell Pick send the same shape. Their results are captured
// (SandbellPickBonus.md, BallGalePickBonus.md); neither feature is built yet.
[Serializable]
public class ServerPickBonusFeature
{
    public bool enabled;
    public int? symbolId;          // the Ball or the Sandbell
    // 3. Counts cells, not reels — two scatters can land on one reel.
    public int triggerCount;
    public int respinReel;
    // Multiples of the total bet, confirmed by the trigger captures.
    public List<double> availablePrizes;
    public double maxWinMultiplier;

    // Sent and not yet bound: "nearMissReels" and "nearMissCountRange", which the near-miss rules of
    // both bonuses need — bind them when those features are built. "triggerProbability" is
    // server-side maths the client has no use for.
}

[Serializable]
public class ServerUIData
{
    // The name is inherited from the payline backends; it holds the symbol table.
    public PaylineData paylines;
}

[Serializable]
public class PaylineData
{
    public List<ServerSymbolInfo> symbols;
}

[Serializable]
public class ServerSymbolInfo
{
    public int id;
    public string name;         // "Wild", "Jack-in-the-box", "Drum", ... — display only, never used to find a role
    // "high", "low", "wild" or "scatter". This is what tells the special symbols apart; the init
    // sends no isSpecialSymbol flag and no displayName. Three symbols are scatters, so which feature
    // each one belongs to comes from the features' symbolId. "description" only repeats the group.
    public string group;
    // The paytable, ASCENDING and ENDING at a full-reel match: with 5 reels, index 0 = 2-of-a-kind,
    // 1 = 3, 2 = 4, 3 = 5. A 0 means that count doesn't pay — only Jack-in-the-box pays on 2. Credits,
    // not cash: one credit is the selected (per-line) bet, so J's 5-of-a-kind (100) pays 1.00 at bet
    // 0.01, confirmed. All zeros on the Wild and the three scatters. The converter reverses it,
    // because the client works descending from a full-reel match.
    public List<double> multiplier;
}

[Serializable]
public class ServerPlayer
{
    public double balance;
}

#endregion

#region Server Communication Models — Spin Result

[Serializable]
public class ServerSpinResponse
{
    public string id = "ResultData";
    public bool success;
    // Row-major and top-level, NOT nested under payload: 3 rows x 5 columns, values as strings.
    public List<List<string>> matrix;
    public ServerPayload payload;
    public ServerPlayerBalance player;
}

[Serializable]
public class ServerPlayerBalance
{
    // Nullable in case the server ever omits it; the converter falls back to a computed balance.
    public double? balance;
}

[Serializable]
public class ServerPayload
{
    // NOT the line wins alone, whatever the name says: it equals grandTotalWin on every capture,
    // including the pick-bonus triggers, where lineWins is empty and it carries the bonus prize.
    // grandTotalWin is the figure to display.
    public double winAmount;
    // One entry per winning PAYLINE. Lines can share cells: three Jack-in-the-box 2-of-a-kinds on
    // lines 3, 9 and 13 all used the same two cells.
    public List<ServerLineWin> lineWins;

    // The spin's total, and the only figure to display. The balance moves by exactly this minus the
    // total bet (6066.65 -> 6066.50 on a 0.05 win at a 0.20 bet).
    public double grandTotalWin;

    // The near-miss respin, decided by the server in this same result: the board landed before the
    // respin and which reel was respun. Null when the spin had none. "matrix" is the board AFTER it.
    public ServerNearMissRespin nearMissRespin;
    // True / 1 on a spin that carried a respin. Bound for completeness; nearMissRespin is what the
    // client acts on.
    public bool isRespin;
    public int respinCount;

    // Free Spins' round state. Sent on the spin that triggers and on every spin of the round; an
    // ordinary base spin has no block at all, which reads as "not in a round" (BonusFreeSpins.md §9.4).
    public ServerFreeSpinsResult freeSpins;

    // The Sandbell Pick, whole, on the spin that triggers it — the pick is never sent to the server
    // (SandbellPickBonus.md §8.2). Absent on every other spin.
    public ServerSandbellPickBonus sandbellPickBonus;

    // The Ball Gale Pick, whole, on the spin that triggers it: every pick, in order, decided with the
    // spin — each click only chooses which gem shows the next one (BallGalePickBonus.md §9.2).
    public ServerBallPickemBonus ballPickemBonus;

    // Sent and deliberately unbound:
    //  - "betAmountPerLine" and "totalBetAmount" (0.01 and 0.2 at the lowest bet): the client
    //    already knows its own bet. Sent on free spins too, where nothing is charged.
    //  - "netReturnRatio" (grandTotalWin over total bet): the controller derives the same ratio.
}

[Serializable]
public class ServerBallPickemBonus
{
    public string featureName;     // "ballPickemBonus"
    public bool triggered;
    // The picks in the order the player makes them; the last has isEnd (and isCollect).
    public List<ServerBallPick> picks;
    // The picks' values summed (× total bet).
    public double totalWinMultiplier;
    // The prize in cash: totalWinMultiplier × total bet. TOTAL WIN.
    public double winInCash;

    // Sent and deliberately unbound: "grid", "gridDimensions" and "centerBox" (the backend says ignore
    // them; "grid" is to be removed), and "triggerPositions" (the Balls' boxes come from the symbol id,
    // as the Drum's do).
}

[Serializable]
public class ServerBallPick
{
    public int pickIndex;
    // An X value from availablePrizes, × total bet; 0 on the COLLECT pick.
    public double multiplier;
    // The last pick: picking ends here.
    public bool isEnd;
    // The COLLECT pick, worth nothing. Always equal to isEnd (backend); each is read for its own job.
    public bool isCollect;

    // "revealedPrize" (= multiplier) and "revealed" ("x15" / "end") are sent and deliberately unbound.
}

[Serializable]
public class ServerSandbellPickBonus
{
    public string featureName;     // "sandbellPickBonus"
    public bool triggered;
    // The winning value, on a cell of the server's choosing — the client puts it on whichever
    // Sandbell the player clicks.
    public ServerPickPosition pickedPosition;
    // One per other Sandbell on the board, stacked ones included: the values the player missed.
    public List<ServerPickPosition> unpickedPositions;
    // The winning value again (× total bet). "prizeMultiplier" is sent too, always equal, and left
    // unbound.
    public double totalWinMultiplier;
    // The prize in cash: winning value × total bet. TOTAL WIN.
    public double winInCash;
}

[Serializable]
public class ServerPickPosition
{
    public int row;
    public int col;
    // An X value from availablePrizes, × total bet.
    public double multiplier;
}

[Serializable]
public class ServerNearMissRespin
{
    public bool triggered;
    // Zero-based reel that was respun: 3 for the Drum, 4 for the Ball and the Sandbell.
    public int reelIndex;
    // Row-major like matrix: the board before the respin. Only reel reelIndex differs from matrix.
    public List<List<string>> originalMatrix;
    // Which feature the near miss was for: "freeSpins", "sandbellPickBonus" — and "ballPickem" for
    // the Ball, which doesn't match its init key (reported).
    public string featureName;

    // "newMatrix" (the board after the respin) is deliberately unbound: it is not always sent, and
    // the top-level matrix is always that board.
}

[Serializable]
public class ServerFreeSpinsResult
{
    // Spins were awarded ON THIS SPIN: the trigger, or a retrigger inside the round.
    public bool triggered;
    // This spin was played inside the round: false on the base-game spin that triggers, true on
    // every free spin. What the round's last spin sends is not captured yet.
    public bool active;
    // Spins left after this one. A retrigger's spins are already folded in, so it can go up.
    public int spinsRemaining;
    // Spins awarded in the round so far, retriggers included.
    public int totalSpins;
    // The round's running total in cash, this spin included. Excludes the trigger spin's own win,
    // which was paid on that spin.
    public double totalWinCash;
}

[Serializable]
public class ServerLineWin
{
    // Zero-based index into the init's lines. Confirmed: lineIndex 18's matchedPositions are
    // lines[18] = [2,0,0,0,2], cell for cell.
    public int lineIndex;
    // The paying cells, left to right, one per matched reel.
    public List<ServerCell> matchedPositions;
    // The symbol in each matched cell, Wilds included: [0,6,6] pays as three 10s.
    public List<int> symbolIds;
    public int matchLength;        // reels matched from the left: 2 to 5
    // Already inside winInCash; 1 on every captured win.
    public double lineMultiplier;
    // The line's win in cash: basePayout x lineMultiplier x the per-line bet.
    public double winInCash;

    // "basePayout" and "winInCredits" are sent and deliberately unbound: winInCash already carries
    // them, converted.
}

[Serializable]
public class ServerCell
{
    public int row;
    public int col;
}

#endregion

#region Client-Side Spin Request

[Serializable]
public class SpinRequest
{
    public string type = "SPIN";
    public SpinPayload payload;
}

[Serializable]
public class SpinPayload
{
    // betIndex is the only field with a confirmed effect. The server owns free-spin state, so
    // there is no isFreeSpin flag to send. The mock client also sends "spins": 100; nobody knows
    // what it does, and this client deliberately does not send it.
    public int betIndex;
}

#endregion

#region Game Configuration (Client Side Converted)

[Serializable]
public class GameConfig
{
    public int reelCount = 5;
    // Every row the server sends is live and pays.
    public int rowCount = 3;

    // Total bet = selected bet x activeLine: the init's totalLines (20), since every payline is always
    // played. Also what the line-count label shows.
    public int activeLine = 20;

    // The paylines, one row index per reel, row 0 at the top. A win's WinLine.lineId indexes this.
    public List<List<int>> paylines;

    public List<double> availableBets;
    public List<SymbolInfo> symbols;

    // Resolved from the init symbol table. -1 means "not present", so an unresolved role can never
    // collide with a real symbol id the way a 0 default would — and -1 stays meaningful downstream:
    // SocketIOManager keeps both off the pre-spin placeholder board, and reads -1 as "no such
    // symbol, leave nothing out".
    public int wildSymbolId = -1;
    // A Gold of Luck leftover: that game had one scatter, Wild Jokers has three, so this resolves to
    // whichever comes last in the symbol table (the Sandbell). Only SocketIOManager's placeholder
    // opening board still reads it; everything else uses the per-feature ids below.
    public int scatterSymbolId = -1;

    // Each feature's symbol, from features.<feature>.symbolId. -1 when the init doesn't name one.
    public int freeSpinsSymbolId = -1;      // the Drum
    public int ballBonusSymbolId = -1;      // the Ball
    public int sandbellBonusSymbolId = -1;  // the Sandbell

    // Free Spins, from features.freeSpins. Reels are zero-based. Empty lists / -1 when not sent, which
    // switches the Drum anticipation off rather than guessing.
    public int freeSpinsCount;                                   // spins per trigger: 10
    public List<int> freeSpinsTriggerReels = new List<int>();     // [1,2,3]: a Drum on each triggers
    public List<int> freeSpinsNearMissReels = new List<int>();    // [1,2]: a Drum on each is the near miss
    public int freeSpinsRespinReel = -1;                          // 3: the reel the near miss respins
}

[Serializable]
public class SymbolInfo
{
    public int id;
    public string name;
    // Descending from a full-reel match: index 0 = reelCount-of-a-kind, 1 = one fewer, and so on,
    // ending at minMatch — counts that don't pay are left off. In credits — multiply by the selected
    // (per-line) bet for cash.
    public List<double> multipliers;

    public bool isWild;
    public bool isScatter;
    // True for the Wild and the three scatters.
    public bool isSpecial;

    // Fewest matching symbols that pay: 2 for Jack-in-the-box, 3 for the rest. 0 when the symbol has
    // no paytable (the Wild and the scatters).
    public int minMatch;
}

#endregion

#region Player & Game State (Client Side)

[Serializable]
public class PlayerData
{
    public double balance;
    public int currentBetIndex;
}

[Serializable]
public class SpinResult
{
    // Column-major: [reel][row], the transpose of the server's row-major matrix.
    public List<List<int>> resultMatrix;

    // Everything the spin paid, from grandTotalWin. This is the figure to show and the one the
    // balance moved by.
    public double winAmount;
    // payload.winAmount. Despite the wire name it is not the line wins alone — it equals winAmount on
    // every capture, a pick-bonus prize included — so nothing reads it.
    public double lineWinAmount;

    // One entry per winning payline.
    public List<WinLine> winLines;
    public PlayerData playerData;

    // The free-spins state, from payload.freeSpins. Never null.
    public FreeGameData freeGame;

    // The near-miss respin, or null when the spin had none. resultMatrix is the board after it.
    public RespinData respin;

    // The Sandbell Pick this spin won, or null.
    public SandbellPickData sandbellPick;

    // The Ball Gale Pick this spin won, or null.
    public BallGalePickData ballGalePick;
}

/// <summary>
/// A won Sandbell Pick (SandbellPickBonus.md §5, §8.2): which cells hold Sandbells and the values to
/// reveal. The winning value goes on the Sandbell the player clicks; the missed values on the rest.
/// </summary>
[Serializable]
public class SandbellPickData
{
    // Flat indices (row * reelCount + col) of every Sandbell, in board order: left to right, then top
    // to bottom within a reel.
    public List<int> cells;
    // The winning X value (× total bet).
    public double winValue;
    // The missed X values, in the order the server sent them — one per other Sandbell.
    public List<double> missedValues;
    // The prize in cash, the server's figure: TOTAL WIN.
    public double prize;
}

/// <summary>
/// A won Ball Gale Pick (BallGalePickBonus.md §6, §9.2): the picks in the order they are revealed —
/// the n-th click shows picks[n] — and the prize. Which gem shows which pick is the player's click.
/// </summary>
[Serializable]
public class BallGalePickData
{
    // In pickIndex order. The last has isEnd.
    public List<BallPick> picks;
    // The prize in cash, the server's figure: TOTAL WIN. Never the client's sum of the values.
    public double prize;
}

/// <summary>One revealed gem: an X value, or COLLECT.</summary>
[Serializable]
public class BallPick
{
    // The X value (× total bet). 0 on COLLECT.
    public double value;
    // Shows COLLECT instead of a value, and adds nothing to the total.
    public bool isCollect;
    // Picking ends after this one.
    public bool isEnd;
}

/// <summary>
/// A near-miss respin decided by the server: land originalMatrix, then respin reelIndex onto the
/// result's final board.
/// </summary>
[Serializable]
public class RespinData
{
    // Zero-based reel to respin.
    public int reelIndex;
    // Column-major like resultMatrix: the board the reels land on before the respin.
    public List<List<int>> originalMatrix;
    // The feature the near miss was for, as the server names it ("freeSpins", ...).
    public string featureName;
}

/// <summary>
/// One winning payline.
/// </summary>
[Serializable]
public class WinLine
{
    // The payline that won: an index into GameConfig.paylines.
    public int lineId;
    // The symbol the line pays for. A Wild standing in never counts here, so a line of Wild, 10, 10
    // reads as the 10.
    public int symbolId;
    // Flat indices into the active grid: row * reelCount + col. The paying cells, left to right.
    public List<int> positions;
    public double winAmount;

    // Reels matched from the left: 2 to 5.
    public int matchCount;
    // Already inside winAmount. 1 when there is none.
    public double multiplier;
}

/// <summary>
/// The Free Spins facts for one spin, from payload.freeSpins (BonusFreeSpins.md §9.4).
/// A base-game trigger is spinsAwarded and not active; a retrigger is spinsAwarded and active.
/// </summary>
[Serializable]
public class FreeGameData
{
    // The block was sent. False on an ordinary base spin, and every other field is then zero.
    public bool present;

    // Spins were awarded on this spin — the trigger, or a retrigger inside the round.
    public bool spinsAwarded;

    // This spin was played inside the round. False on the base-game spin that triggers.
    public bool active;

    // Spins left AFTER this one. Retriggers are folded in, so it can go up as well as down.
    public int spinsRemaining;

    // Spins awarded in the round so far, retriggers included. The counter's Y; its X, the spins
    // played, is totalSpins - spinsRemaining.
    public int totalSpins;

    // The round's running total, server-authoritative, this spin included. Excludes the trigger
    // spin's own win.
    public double roundWin;
}

#endregion

#region Enums

public enum GameState
{
    Initializing,
    Idle,
    Spinning,
    Stopping,
    ShowingWin
}

public enum SpinSpeed
{
    Normal,
    Turbo,
    QuickSpin
}

public enum WinPopupType
{
    BigWin
}

#endregion

#region Helper Classes for Conversion

/// <summary>
/// The single seam between server JSON and client types. Wire shapes change here and nowhere else,
/// so the view and controller layers never see a backend revision.
/// </summary>
public static class InitDataConverter
{
    // Used only if the init carries neither totalLines nor any lines. Total bet = selected bet x 20,
    // which matches every captured balance: each spin at bet 0.01 cost exactly 0.20.
    private const int FallbackBetMultiplier = 20;

    internal static GameConfig ConvertToGameConfig(InitData serverData)
    {
        var gameData = serverData?.gameData;
        var serverSymbols = serverData?.uiData?.paylines?.symbols;
        var features = serverData?.features;

        int reelCount = (gameData?.lines != null && gameData.lines.Count > 0 && gameData.lines[0] != null)
            ? gameData.lines[0].Count
            : 5;

        var config = new GameConfig
        {
            reelCount = reelCount,
            rowCount = 3,
            activeLine = ResolveBetMultiplier(gameData),
            paylines = ConvertPaylines(gameData?.lines, reelCount, 3),
            availableBets = gameData?.bets,
            symbols = new List<SymbolInfo>()
        };

        if (config.availableBets == null || config.availableBets.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no bet levels — the bet controls will do nothing.");
        }

        if (serverSymbols == null)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no symbol table — symbol art and paytables will be unresolved.");
            return config;
        }

        foreach (var serverSymbol in serverSymbols)
        {
            if (serverSymbol == null) continue;

            // The role comes from "group", never the name.
            string group = (serverSymbol.group ?? string.Empty).Trim().ToLowerInvariant();

            var multipliers = ConvertPaytable(serverSymbol, reelCount);

            var symbolInfo = new SymbolInfo
            {
                id = serverSymbol.id,
                name = serverSymbol.name,
                multipliers = multipliers,
                isWild = group == "wild",
                isScatter = group == "scatter",
                minMatch = multipliers.Count > 0 ? reelCount - multipliers.Count + 1 : 0
            };
            symbolInfo.isSpecial = symbolInfo.isWild || symbolInfo.isScatter;

            // An unknown group means the backend added a role this client has never heard of.
            if (group != "high" && group != "low" && !symbolInfo.isSpecial)
            {
                UnityEngine.Debug.LogError($"[InitDataConverter] Symbol id {symbolInfo.id} (name '{symbolInfo.name}') has unrecognised group '{serverSymbol.group}' — it will be treated as an ordinary symbol.");
            }

            config.symbols.Add(symbolInfo);

            if (symbolInfo.isWild) config.wildSymbolId = symbolInfo.id;
            if (symbolInfo.isScatter) config.scatterSymbolId = symbolInfo.id;
        }

        // The Wild comes from its group above; features.wild.symbolId names it too, so the two are
        // only cross-checked.
        int? featureWildId = features?.wild?.symbolId;
        if (featureWildId.HasValue && featureWildId.Value != config.wildSymbolId)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.wild names symbol {featureWildId.Value} but the symbol table's wild is {config.wildSymbolId} — using the symbol table's.");
        }

        config.freeSpinsSymbolId = ResolveFeatureSymbolId(features?.freeSpins?.symbolId, "freeSpins", config);
        config.ballBonusSymbolId = ResolveFeatureSymbolId(features?.ballPickemBonus?.symbolId, "ballPickemBonus", config);
        config.sandbellBonusSymbolId = ResolveFeatureSymbolId(features?.sandbellPickBonus?.symbolId, "sandbellPickBonus", config);

        var freeSpins = features?.freeSpins;
        if (freeSpins != null)
        {
            config.freeSpinsCount = freeSpins.spinsCount;
            if (freeSpins.triggerReels != null) config.freeSpinsTriggerReels = new List<int>(freeSpins.triggerReels);
            if (freeSpins.nearMissReels != null) config.freeSpinsNearMissReels = new List<int>(freeSpins.nearMissReels);
            config.freeSpinsRespinReel = freeSpins.respinReel;
        }
        else
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no features.freeSpins — the Drum anticipation is off.");
        }

        return config;
    }

    // totalLines is the multiplier — confirmed against balances. The line table is the fallback, and
    // the two are checked against each other, since a mismatch means one of them is wrong.
    private static int ResolveBetMultiplier(ServerGameData gameData)
    {
        int lineCount = gameData?.lines?.Count ?? 0;

        if (gameData != null && gameData.totalLines > 0)
        {
            if (lineCount > 0 && lineCount != gameData.totalLines)
            {
                UnityEngine.Debug.LogError($"[InitDataConverter] Init says totalLines {gameData.totalLines} but sends {lineCount} lines — total bet uses totalLines.");
            }
            return gameData.totalLines;
        }

        int fallback = lineCount > 0 ? lineCount : FallbackBetMultiplier;
        UnityEngine.Debug.LogError($"[InitDataConverter] Init carried no totalLines — total bet assumed to be bet x {fallback}.");
        return fallback;
    }

    // Kept as sent, so a win's lineIndex still points at the right line. A malformed line is logged,
    // not dropped: dropping it would shift every line after it.
    private static List<List<int>> ConvertPaylines(List<List<int>> lines, int reelCount, int rowCount)
    {
        var paylines = lines ?? new List<List<int>>();

        if (paylines.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Init carried no paylines — line wins cannot be traced.");
            return paylines;
        }

        for (int i = 0; i < paylines.Count; i++)
        {
            var line = paylines[i];
            if (line == null || line.Count != reelCount || line.Exists(row => row < 0 || row >= rowCount))
            {
                UnityEngine.Debug.LogError($"[InitDataConverter] Payline {i} is not {reelCount} rows within 0..{rowCount - 1}: [{(line != null ? string.Join(",", line) : "null")}].");
            }
        }

        return paylines;
    }

    // The wire paytable ascends to a full-reel match; the client works descending from one, so it is
    // reversed. Counts that don't pay are zeros on the wire (A sends 0 for 2-of-a-kind) and are cut
    // off the end, so the list stops at the fewest that pay and nothing downstream shows a "pays 0" row.
    private static List<double> ConvertPaytable(ServerSymbolInfo serverSymbol, int reelCount)
    {
        var multipliers = new List<double>(serverSymbol.multiplier ?? new List<double>());

        if (multipliers.Count > reelCount)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] Symbol id {serverSymbol.id} sends {multipliers.Count} payouts for {reelCount} reels — the extra short counts are ignored.");
        }

        multipliers.Reverse();
        if (multipliers.Count > reelCount) multipliers.RemoveRange(reelCount, multipliers.Count - reelCount);
        while (multipliers.Count > 0 && multipliers[multipliers.Count - 1] <= 0) multipliers.RemoveAt(multipliers.Count - 1);

        return multipliers;
    }

    // A feature's symbol, checked against the symbol table. Missing or unknown reads as -1, never 0,
    // which is the Wild.
    private static int ResolveFeatureSymbolId(int? symbolId, string featureName, GameConfig config)
    {
        if (!symbolId.HasValue)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.{featureName} names no symbolId — its trigger symbol is unknown.");
            return -1;
        }

        SymbolInfo symbol = config.symbols.Find(s => s.id == symbolId.Value);
        if (symbol == null)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.{featureName} names symbol {symbolId.Value}, which is not in the symbol table.");
            return -1;
        }

        if (!symbol.isScatter)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] features.{featureName} names symbol {symbolId.Value} ('{symbol.name}'), which is not a scatter.");
        }

        return symbolId.Value;
    }

    internal static PlayerData ConvertToPlayerData(ServerPlayer serverPlayer, int defaultBetIndex = 0)
    {
        return new PlayerData
        {
            balance = serverPlayer != null ? serverPlayer.balance : 0,
            currentBetIndex = defaultBetIndex
        };
    }

    /// <summary>
    /// Converts one spin response into the client's SpinResult.
    /// betAmount is accepted for signature stability but no longer used: the server always sends
    /// the post-spin balance, and the fallback below works off the already-deducted local balance.
    /// </summary>
    internal static SpinResult ConvertServerResponseToSpinResult(ServerSpinResponse serverResponse, double currentBalance, double betAmount, GameConfig gameConfig)
    {
        var payload = serverResponse?.payload;

        // grandTotalWin is the spin's total and passes straight through. payload.winAmount is kept
        // separately and never used as the figure to show. Nothing is summed here.
        double totalWin = payload?.grandTotalWin ?? 0;
        double newBalance = serverResponse?.player?.balance ?? CalculateNewBalance(currentBalance, totalWin);

        return new SpinResult
        {
            resultMatrix = ConvertMatrixToColumns(serverResponse?.matrix, gameConfig),
            winAmount = totalWin,
            lineWinAmount = payload?.winAmount ?? 0,
            winLines = ConvertLineWins(payload?.lineWins, gameConfig),

            playerData = new PlayerData
            {
                balance = newBalance,
                currentBetIndex = 0
            },

            freeGame = ConvertFreeGame(payload?.freeSpins),
            respin = ConvertRespin(payload?.nearMissRespin, gameConfig),
            sandbellPick = ConvertSandbellPick(payload?.sandbellPickBonus, gameConfig),
            ballGalePick = ConvertBallGalePick(payload?.ballPickemBonus)
        };
    }

    // Null unless the bonus was won with at least one pick. The picks are put in pickIndex order; the
    // server's order already is, but nothing depends on that.
    private static BallGalePickData ConvertBallGalePick(ServerBallPickemBonus serverBonus)
    {
        if (serverBonus == null || !serverBonus.triggered) return null;

        var picks = new List<ServerBallPick>();
        if (serverBonus.picks != null)
        {
            foreach (ServerBallPick pick in serverBonus.picks)
            {
                if (pick != null) picks.Add(pick);
            }
        }

        if (picks.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] ballPickemBonus carried no picks — bonus ignored.");
            return null;
        }

        picks.Sort((a, b) => a.pickIndex.CompareTo(b.pickIndex));

        var converted = new List<BallPick>(picks.Count);
        foreach (ServerBallPick pick in picks)
        {
            converted.Add(new BallPick
            {
                value = pick.multiplier,
                isCollect = pick.isCollect,
                isEnd = pick.isEnd
            });
        }

        return new BallGalePickData
        {
            picks = converted,
            prize = serverBonus.winInCash
        };
    }

    // Null unless the bonus was won. The cells come from the server's positions (they match the
    // board); only the values matter beyond that — which Sandbell shows which is the click's.
    private static SandbellPickData ConvertSandbellPick(ServerSandbellPickBonus serverBonus, GameConfig gameConfig)
    {
        if (serverBonus == null || !serverBonus.triggered) return null;

        if (serverBonus.pickedPosition == null)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] sandbellPickBonus carried no pickedPosition — bonus ignored.");
            return null;
        }

        var cells = new List<int>();
        var missedValues = new List<double>();

        if (TryGetFlatIndex(serverBonus.pickedPosition.row, serverBonus.pickedPosition.col, gameConfig, "sandbellPickBonus.pickedPosition", out int pickedCell))
        {
            cells.Add(pickedCell);
        }

        if (serverBonus.unpickedPositions != null)
        {
            for (int i = 0; i < serverBonus.unpickedPositions.Count; i++)
            {
                ServerPickPosition position = serverBonus.unpickedPositions[i];
                if (position == null) continue;

                missedValues.Add(position.multiplier);
                if (TryGetFlatIndex(position.row, position.col, gameConfig, $"sandbellPickBonus.unpickedPositions[{i}]", out int cell))
                {
                    cells.Add(cell);
                }
            }
        }

        // Board order: by reel, then by row — the order the missed values are laid out in.
        int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
        cells.Sort((a, b) =>
        {
            int byReel = (a % reelCount).CompareTo(b % reelCount);
            return byReel != 0 ? byReel : (a / reelCount).CompareTo(b / reelCount);
        });

        return new SandbellPickData
        {
            cells = cells,
            winValue = serverBonus.pickedPosition.multiplier,
            missedValues = missedValues,
            prize = serverBonus.winInCash
        };
    }

    // Never returns null, so the controller can read it without guarding every access. A missing
    // block is an ordinary base spin: not in a round, nothing awarded.
    private static FreeGameData ConvertFreeGame(ServerFreeSpinsResult serverFreeSpins)
    {
        if (serverFreeSpins == null) return new FreeGameData();

        return new FreeGameData
        {
            present = true,
            spinsAwarded = serverFreeSpins.triggered,
            active = serverFreeSpins.active,
            spinsRemaining = serverFreeSpins.spinsRemaining,
            totalSpins = serverFreeSpins.totalSpins,
            roundWin = serverFreeSpins.totalWinCash
        };
    }

    // Null unless the server actually respun a reel. A respin without a usable board or reel is
    // logged and dropped: the client then just lands the final board, which is never wrong.
    private static RespinData ConvertRespin(ServerNearMissRespin serverRespin, GameConfig gameConfig)
    {
        if (serverRespin == null || !serverRespin.triggered) return null;

        int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
        if (serverRespin.reelIndex < 0 || serverRespin.reelIndex >= reelCount)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] nearMissRespin.reelIndex {serverRespin.reelIndex} is outside the {reelCount} reels — respin ignored.");
            return null;
        }

        if (serverRespin.originalMatrix == null || serverRespin.originalMatrix.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] nearMissRespin carried no originalMatrix — respin ignored.");
            return null;
        }

        return new RespinData
        {
            reelIndex = serverRespin.reelIndex,
            originalMatrix = ConvertMatrixToColumns(serverRespin.originalMatrix, gameConfig),
            featureName = serverRespin.featureName
        };
    }

    // The one place a row and column become a flat index, so the bounds check cannot drift between
    // callers.
    private static bool TryGetFlatIndex(int row, int col, GameConfig gameConfig, string source, out int flatIndex)
    {
        flatIndex = -1;

        int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
        int rowCount = gameConfig != null ? gameConfig.rowCount : 3;

        if (row < 0 || row >= rowCount || col < 0 || col >= reelCount)
        {
            UnityEngine.Debug.LogError($"[InitDataConverter] {source} is outside the {rowCount}x{reelCount} grid — cell skipped.");
            return false;
        }

        flatIndex = row * reelCount + col;
        return true;
    }

    // Server matrix is row-major (matrix[row][col]); the client works column-major ([reel][row]),
    // so this transposes. No padding rows to skip — every row the server sends is live.
    private static List<List<int>> ConvertMatrixToColumns(List<List<string>> serverMatrix, GameConfig gameConfig)
    {
        int reelCount = gameConfig != null ? gameConfig.reelCount : 5;
        int rowCount = gameConfig != null ? gameConfig.rowCount : 3;

        if (serverMatrix == null || serverMatrix.Count == 0)
        {
            UnityEngine.Debug.LogError("[InitDataConverter] Spin response carried no matrix.");
            return GenerateDefaultMatrix(reelCount, rowCount);
        }

        var matrix = new List<List<int>>();

        for (int col = 0; col < reelCount; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                if (row >= serverMatrix.Count || serverMatrix[row] == null || col >= serverMatrix[row].Count)
                {
                    UnityEngine.Debug.LogError($"[InitDataConverter] matrix has no cell at row {row}, col {col}.");
                    column.Add(0);
                    continue;
                }

                if (!int.TryParse(serverMatrix[row][col], out int symbolId))
                {
                    UnityEngine.Debug.LogError($"[InitDataConverter] Could not parse symbol at row {row}, col {col}.");
                    symbolId = 0;
                }

                column.Add(symbolId);
            }
            matrix.Add(column);
        }

        return matrix;
    }

    // One WinLine per winning payline. The server already says which cells pay and what is in them,
    // so nothing is rebuilt from the line table.
    private static List<WinLine> ConvertLineWins(List<ServerLineWin> lineWins, GameConfig gameConfig)
    {
        var winLines = new List<WinLine>();
        if (lineWins == null) return winLines;

        for (int i = 0; i < lineWins.Count; i++)
        {
            var lineWin = lineWins[i];
            if (lineWin == null) continue;

            var flatPositions = new List<int>();
            if (lineWin.matchedPositions != null)
            {
                foreach (var cell in lineWin.matchedPositions)
                {
                    if (cell == null) continue;

                    if (TryGetFlatIndex(cell.row, cell.col, gameConfig, $"lineWins[{i}] cell ({cell.row},{cell.col})", out int flatIndex))
                    {
                        flatPositions.Add(flatIndex);
                    }
                }
            }

            winLines.Add(new WinLine
            {
                lineId = lineWin.lineIndex,
                symbolId = GetPayingSymbolId(lineWin.symbolIds, gameConfig),
                positions = flatPositions,
                winAmount = lineWin.winInCash,
                matchCount = lineWin.matchLength,
                // A missing field deserializes as 0, which would read as "pays nothing"; treat it
                // as no multiplier. Never clamps a real value: every line is x1 or more.
                multiplier = Math.Max(1.0, lineWin.lineMultiplier)
            });
        }

        return winLines;
    }

    // The symbol a line pays for: the first matched symbol that isn't the Wild, since a Wild standing
    // in is listed as itself ([0,6,6] pays as 10s). A line of nothing but Wilds pays for the Wild.
    // -1 when the server listed no symbols.
    private static int GetPayingSymbolId(List<int> symbolIds, GameConfig gameConfig)
    {
        if (symbolIds == null || symbolIds.Count == 0) return -1;

        int wildId = gameConfig != null ? gameConfig.wildSymbolId : -1;
        foreach (int id in symbolIds)
        {
            if (id != wildId) return id;
        }

        return symbolIds[0];
    }

    private static List<List<int>> GenerateDefaultMatrix(int reelCount, int rowCount)
    {
        var matrix = new List<List<int>>();
        for (int col = 0; col < reelCount; col++)
        {
            var column = new List<int>();
            for (int row = 0; row < rowCount; row++)
            {
                column.Add(0);
            }
            matrix.Add(column);
        }
        return matrix;
    }

    // currentBalance already reflects StartSpin()'s optimistic upfront deduction of the total bet,
    // so only the win is added back. Only reached if the server omits a balance.
    private static double CalculateNewBalance(double currentBalance, double winAmount)
    {
        return currentBalance + winAmount;
    }
}

#endregion
