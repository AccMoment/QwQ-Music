using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using QwQ_Music.Common.Managers;
using QwQ_Music.Common.Services;
using QwQ_Music.ViewModels.Bases;
using QwQ_Music.ViewModels.Dialogs;
using QwQ_Music.Views.Dialogs;
using Ursa.Controls;

namespace QwQ_Music.ViewModels.Pages;

public partial class SoundEffectConfigViewModel() : NavigationViewModel("音效") {
    public SoundModifierManager SoundModifierManager => SoundModifierManager.Default;

    [RelayCommand]
    private void OpenSoundEffectManagerPanel() {
        var options = new OverlayDialogOptions { Title = "管理音效" };

        OverlayDialog.ShowCustomModal<ManageSoundModifier, ManageSoundEffectViewModel, bool>(
                         new ManageSoundEffectViewModel(),
                         options: options)
                     .ContinueWith(task => {
                         if (task is not { IsCompletedSuccessfully: true, Result: true })
                             return;

                         SoundModifierManager.Clear();

                         foreach (var builtInSoundEffect in
                                  SoundModifierManager.SoundEffectConfig.BuiltInSoundEffects) {
                             if (builtInSoundEffect.Value) {
                                 SoundModifierManager.LoadModifier(builtInSoundEffect.Key);
                             } else {
                                 SoundModifierManager.UnLoadModifier(builtInSoundEffect.Key);
                             }
                         }
                     })
                     .ContinueWith(LoggerService.HandleException)
                     .ConfigureAwait(false);
    }
}