using SoundFlow.Abstracts;

namespace QwQ_Music.Common.Interfaces;

public interface ISoundModifierModel
{
    public SoundModifier? Modifier { get; }

    public string Name { get; }

    public bool Enabled { get; set; }

    public void Revoke();
}

public interface ISoundModifierModel<TModifier> : ISoundModifierModel
    where TModifier : SoundModifier
{
    public new TModifier? Modifier { get; }

    ISoundModifierModel<TModifier> Initialize(TModifier modifier);
}