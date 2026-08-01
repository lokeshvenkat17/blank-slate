namespace BlankSlate.Models;

/// <summary>Edit &gt; Convert Case to.</summary>
public enum CaseKind
{
    Upper, Lower,
    ProperForce, ProperBlend,
    SentenceForce, SentenceBlend,
    Invert, Random,
}

/// <summary>Edit &gt; Line Operations (sorts are <see cref="SortKind"/>).</summary>
public enum LineOpKind
{
    Duplicate,
    RemoveDuplicates,
    RemoveConsecutiveDuplicates,
    JoinLines,
    MoveUp,
    MoveDown,
    RemoveEmpty,
    RemoveEmptyWithBlank,
    BlankAbove,
    BlankBelow,
    Reverse,
    Randomize,
}

/// <summary>Edit &gt; Line Operations sort variants.</summary>
public enum SortKind
{
    LexAsc, LexDesc,
    LexCiAsc, LexCiDesc,
    LocaleAsc, LocaleDesc,
    IntAsc, IntDesc,
    DecCommaAsc, DecCommaDesc,
    DecDotAsc, DecDotDesc,
    LenAsc, LenDesc,
}

/// <summary>Edit &gt; Blank Operations.</summary>
public enum BlankOpKind
{
    TrimTrailing,
    TrimLeading,
    TrimBoth,
    EolToSpace,
    TrimAll,
    TabToSpace,
    SpaceToTabAll,
    SpaceToTabLeading,
}

/// <summary>Edit &gt; Comment/Uncomment.</summary>
public enum CommentOpKind
{
    ToggleLine,
    SetLine,
    RemoveLine,
    BlockSet,
    BlockRemove,
}
