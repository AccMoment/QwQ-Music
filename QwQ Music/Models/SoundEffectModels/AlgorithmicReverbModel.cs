using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using QwQ_Music.Common.Interfaces;
using SoundFlow.Abstracts;
using SoundFlow.Modifiers;

namespace QwQ_Music.Models.SoundEffectModels;

public class AlgorithmicReverbModel : ObservableObject, ISoundModifierModel<AlgorithmicReverbModifier>
{
    [JsonIgnore] public AlgorithmicReverbModifier? Modifier { get; private set; }

    [JsonIgnore] SoundModifier? ISoundModifierModel.Modifier => Modifier;

    [JsonIgnore] public string Name { get; } = "混响";

    public ISoundModifierModel<AlgorithmicReverbModifier> Initialize(AlgorithmicReverbModifier modifier)
    {
        // 初始化时同步所有属性值到 Modifier
        modifier.Enabled = Enabled;
        modifier.Wet = Wet;
        modifier.RoomSize = RoomSize;
        modifier.Damp = Damp;
        modifier.Width = Width;
        modifier.PreDelay = PreDelay;
        modifier.Mix = Mix;

        Modifier = modifier;

        return this;
    }

    public void Revoke()
    {
        Modifier = null;
    }

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
    }

    /// <summary>
    ///     获取或设置湿声混合量。取值范围被限制在 0 到 1 之间。
    /// </summary>
    public float Wet
    {
        get;
        set
        {
            value = Math.Clamp(value, 0f, 1f);

            if (SetProperty(ref field, value))
            {
                Modifier?.Wet = value;
            }
        }
    } = 0.5f;

    /// <summary>
    ///     获取或设置房间大小。取值范围被限制在 0 到 1 之间。
    /// </summary>
    public float RoomSize
    {
        get;
        set
        {
            value = Math.Clamp(value, 0f, 1f);

            if (SetProperty(ref field, value))
            {
                Modifier?.RoomSize = value;
            }
        }
    } = 0.5f;

    /// <summary>
    ///     获取或设置阻尼系数。取值范围被限制在 0 到 1 之间。
    /// </summary>
    public float Damp
    {
        get;
        set
        {
            value = Math.Clamp(value, 0f, 1f);

            if (SetProperty(ref field, value))
            {
                Modifier?.Damp = value;
            }
        }
    } = 0.5f;

    /// <summary>
    ///     获取或设置立体声宽度。取值范围被限制在 0 到 1 之间。
    /// </summary>
    public float Width
    {
        get;
        set
        {
            value = Math.Clamp(value, 0f, 1f);

            if (SetProperty(ref field, value))
            {
                Modifier?.Width = value;
            }
        }
    } = 1f;

    /// <summary>
    ///     获取或设置预延迟时间（毫秒）。取值范围被限制在 0 到 100 毫秒之间。
    /// </summary>
    public float PreDelay
    {
        get;
        set
        {
            value = Math.Clamp(value, 0f, 100f);

            if (SetProperty(ref field, value))
            {
                Modifier?.PreDelay = value;
            }
        }
    }

    /// <summary>
    ///     获取或设置湿/干混合比例。取值范围被限制在 0 到 1 之间。
    /// </summary>
    public float Mix
    {
        get;
        set
        {
            value = Math.Clamp(value, 0f, 1f);

            if (SetProperty(ref field, value))
            {
                Modifier?.Mix = value;
            }
        }
    } = 0.5f;
}
