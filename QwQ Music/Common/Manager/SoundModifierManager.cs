using Avalonia.Collections;
using Avalonia.Controls;
using QwQ_Music.Common.Interfaces;
using QwQ_Music.Models.ConfigModels;
using QwQ_Music.ViewModels;
using QwQ_Music.Views.SoundEffects;
using SoundFlow.Modifiers;
using SoundFlow.Structs;

namespace QwQ_Music.Common.Manager;

public class SoundModifierManager
{
    public readonly SoundEffectConfig SoundEffectConfig = ConfigManager.SoundModifierConfig.SoundEffectConfig;

    public AudioFormat AudioFormat = MusicPlayerViewModel.Default.AudioFormat;

    private SoundModifierManager()
    {
        Initialize();
    }

    public static SoundModifierManager Default { get; } = new();

    public AvaloniaList<Control> ConfigPanels { get; } = [];

    public AvaloniaList<ISoundModifierModel> SoundModifiers { get; } = [];

    private void Initialize()
    {
        if (SoundEffectConfig.BuiltInSoundEffects.Count == 0)
        {
            SoundEffectConfig.BuiltInSoundEffects.Add("AlgorithmicReverb", true);
            SoundEffectConfig.BuiltInSoundEffects.Add("BassBooster", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("Chorus", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("Compressor", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("Delay ", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("FrequencyBand", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("MultiChannelChorusM", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("ParametricEqualizer", false);
            SoundEffectConfig.BuiltInSoundEffects.Add("TrebleBooster", false);
        }

        InitializeModifier();
    }

    public void InitializeModifier()
    {
        if (SoundEffectConfig.BuiltInSoundEffects.TryGetValue("AlgorithmicReverb", out bool value) && value)
        {            
            var algorithmicReverbModel = SoundEffectConfig.AlgorithmicReverb.Initialize(new AlgorithmicReverbModifier(AudioFormat));
            SoundModifiers.Add(algorithmicReverbModel);
            
            ConfigPanels.Add(new AlgorithmicReverb
            {
                DataContext = algorithmicReverbModel,
            });
        }
    }


    public void LoadModifier()
    {
        
    }

    public void UnLoadModifier()
    {
        
    }
}
