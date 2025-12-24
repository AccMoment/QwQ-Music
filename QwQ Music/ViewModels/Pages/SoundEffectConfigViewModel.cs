using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.ViewModels.Dialogs;
using QwQ_Music.Views.Dialogs;
using Ursa.Controls;

namespace QwQ_Music.ViewModels.Pages;

public partial class SoundEffectConfigViewModel() : NavigationViewModel("音效")
{
    public SoundModifierManager SoundModifierManager => SoundModifierManager.Default;

    [RelayCommand]
    private async Task OpenSoundEffectManagerPanel()
    {
        var options = new OverlayDialogOptions
        {
            Title = "管理音效"
        };

        bool result = await OverlayDialog.ShowCustomModal<ManageSoundModifier, ManageSoundEffectViewModel,bool>(
            new ManageSoundEffectViewModel(), options: options);

        if (result)
        {
            SoundModifierManager.Clear();
            
            foreach (var builtInSoundEffect in SoundModifierManager.SoundEffectConfig.BuiltInSoundEffects)
            {
                if (builtInSoundEffect.Value)
                {
                    SoundModifierManager.LoadModifier(builtInSoundEffect.Key);
                }
                else
                {
                    SoundModifierManager.UnLoadModifier(builtInSoundEffect.Key);
                }
            }
        }
    }
}
