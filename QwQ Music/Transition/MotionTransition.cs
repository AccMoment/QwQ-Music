using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace QwQ_Music.Transition;

public class MotionTransitions {
    public static readonly AttachedProperty<bool> OffsetTransitionProperty =
        AvaloniaProperty.RegisterAttached<MotionTransitions, Visual, bool>(
            "OffsetTransition",
            defaultValue: false,
            inherits: true);

    public static readonly AttachedProperty<bool> SizeTransitionProperty =
        AvaloniaProperty.RegisterAttached<MotionTransitions, Visual, bool>(
            "SizeTransition",
            defaultValue: false,
            inherits: true);

    public static readonly AttachedProperty<int> OffsetDurationProperty =
        AvaloniaProperty.RegisterAttached<MotionTransitions, Visual, int>(
            "OffsetDuration",
            defaultValue: 400,
            inherits: true);

    public static readonly AttachedProperty<int> SizeDurationProperty =
        AvaloniaProperty.RegisterAttached<MotionTransitions, Visual, int>(
            "SizeDuration",
            defaultValue: 400,
            inherits: true);

    // 监听属性变化
    static MotionTransitions() {
        OffsetTransitionProperty.Changed.AddClassHandler<Visual, bool>(OnChanged);
        SizeTransitionProperty.Changed.AddClassHandler<Visual, bool>(OnChanged);
    }

    private static void OnChanged(Visual? target, AvaloniaPropertyChangedEventArgs<bool> args) {
        if (target is null)
            return;

        var visual = ElementComposition.GetElementVisual(target);
        if (visual?.Compositor == null)
            return;

        var compositor = visual.Compositor;
        visual.ImplicitAnimations ??= compositor.CreateImplicitAnimationCollection();

        if (GetOffsetTransition(target)) {
            visual.ImplicitAnimations["Offset"] =
                PredefinedAnimations.CreateOffsetAnimation(compositor, GetOffsetDuration(target));
        } else {
            visual.ImplicitAnimations.Remove("Offset");
        }

        if (GetSizeTransition(target)) {
            visual.ImplicitAnimations["Size"] =
                PredefinedAnimations.CreateSizeAnimation(compositor, GetSizeDuration(target));
        } else {
            visual.ImplicitAnimations.Remove("Size");
        }

        if (visual.ImplicitAnimations.Count == 0) {
            visual.ImplicitAnimations = null;
        }
    }

    public static void SetOffsetTransition(Visual element, bool value) =>
        element.SetValue(OffsetTransitionProperty, value);

    public static bool GetOffsetTransition(Visual element) => element.GetValue(OffsetTransitionProperty);

    public static void SetSizeTransition(Visual element, bool value) => element.SetValue(SizeTransitionProperty, value);

    public static bool GetSizeTransition(Visual element) => element.GetValue(SizeTransitionProperty);
    public static void SetOffsetDuration(Visual element, int value) => element.SetValue(OffsetDurationProperty, value);

    public static int GetOffsetDuration(Visual element) => element.GetValue(OffsetDurationProperty);

    public static void SetSizeDuration(Visual element, int value) => element.SetValue(SizeDurationProperty, value);

    public static int GetSizeDuration(Visual element) => element.GetValue(SizeDurationProperty);
}

public static class PredefinedAnimations {
    public static KeyFrameAnimation CreateOffsetAnimation(Compositor compositor, int duration) {
        // 位置变化动画
        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        offsetAnimation.Target = "Offset";
        offsetAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");

        return offsetAnimation;
    }

    public static KeyFrameAnimation CreateSizeAnimation(Compositor compositor, int duration) {
        // 尺寸变化动画
        var sizeAnimation = compositor.CreateVector2KeyFrameAnimation();
        sizeAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        sizeAnimation.Target = "Size";
        sizeAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
        return sizeAnimation;
    }
}