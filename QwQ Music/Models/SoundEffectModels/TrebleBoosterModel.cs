using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Interfaces;
using SoundFlow.Abstracts;
using SoundFlow.Modifiers;
using SoundFlow.Structs;

namespace QwQ_Music.Models.SoundEffectModels;

public class TrebleBoosterModel : ObservableObject, ISoundModifierModel<TrebleBoosterModifier>
{
    [JsonIgnore] public TrebleBoosterModifier? Modifier { get; private set; }

    [JsonIgnore] SoundModifier? ISoundModifierModel.Modifier => Modifier;

    [JsonIgnore] public string Name { get; } = "高音增强";

    public void Initialize(AudioFormat audioFormat)
    {
        var modifier = new TrebleBoosterModifier(audioFormat)
        {
            Cutoff = Cutoff,
            BoostGain = BoostGain,
            Enabled = Enabled
        };

        Modifier = modifier;
    }

    public void Revoke()
    {
        Modifier = null;
    }

    /// <summary>
    /// 获取或设置是否启用效果器。
    /// </summary>
    public bool Enabled
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                Modifier?.Enabled = value;
            }
        }
    } = true;

    /// <summary>
    /// 获取或设置截止频率（Hz）。
    /// </summary>
    public float Cutoff
    {
        get;
        set
        {
            if (SetProperty(ref field, Math.Min(20000, value)))
            {
                Modifier?.Cutoff = Math.Min(20000, value);
            }
        }
    } = 4000f;

    /// <summary>
    /// 获取或设置增益（dB）。
    /// </summary>
    public float BoostGain
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) 
                return;

            if (Modifier == null) 
                return;

            // 将 dB 转换为线性增益
            float linearGain = MathF.Pow(10, value / 20f);
            Modifier.BoostGain = linearGain;
        }
    } = 6f;
}