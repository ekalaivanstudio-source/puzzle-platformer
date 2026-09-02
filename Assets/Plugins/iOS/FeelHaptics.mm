//  FeelHaptics.mm
//
//  The iOS half of HapticService (Assets/MainGame/Scripts/Feel/HapticService.cs).
//  This file and the `#if UNITY_IOS` DllImport block in that script belong together:
//  deleting one without the other breaks the iOS link step.
//
//  Why a native file at all: Unity's own Handheld.Vibrate() maps to the old
//  AudioServicesPlaySystemSound vibrate, which is a single ~400ms buzz of the whole
//  phone — far too heavy for a footstep and impossible to vary. Everything below goes
//  through the Taptic engine instead, so a light tick really is lighter than a heavy one
//  and the phone stays quiet on devices that have no Taptic hardware.
//
//  The generators are created once and kept: they are cheap to hold, and re-creating one
//  per call both wastes the `prepare` warm-up (the reason a prepared generator fires with
//  no latency) and would need release bookkeeping that differs between ARC and manual
//  reference counting. Static, never-released objects are correct under both, which
//  matters because Unity does not compile plugin sources with ARC by default.

#import <UIKit/UIKit.h>

// Mirrors HapticPattern.cs. Keep the two in step.
typedef NS_ENUM(int, FeelHapticPattern) {
    FeelHapticNone          = 0,
    FeelHapticSelection     = 1,
    FeelHapticLightImpact   = 2,
    FeelHapticMediumImpact  = 3,
    FeelHapticHeavyImpact   = 4,
    FeelHapticRigidImpact   = 5,
    FeelHapticSoftImpact    = 6,
    FeelHapticSuccess       = 7,
    FeelHapticWarning       = 8,
    FeelHapticFailure       = 9,
};

static UISelectionFeedbackGenerator*    gSelection    = nil;
static UINotificationFeedbackGenerator* gNotification = nil;
static UIImpactFeedbackGenerator*       gLight        = nil;
static UIImpactFeedbackGenerator*       gMedium       = nil;
static UIImpactFeedbackGenerator*       gHeavy        = nil;
static UIImpactFeedbackGenerator*       gRigid        = nil;
static UIImpactFeedbackGenerator*       gSoft         = nil;

// Built on the first haptic rather than at launch, so a game that never vibrates never
// pays for them. UIKit objects must be touched on the main thread; every entry point
// below is called from Unity's main thread, which is the UI thread on iOS.
static void FeelHapticsPrepare(void)
{
    if (gSelection != nil) return;

    if (@available(iOS 10.0, *)) {
        gSelection    = [[UISelectionFeedbackGenerator alloc] init];
        gNotification = [[UINotificationFeedbackGenerator alloc] init];
        gLight        = [[UIImpactFeedbackGenerator alloc]
                            initWithStyle:UIImpactFeedbackStyleLight];
        gMedium       = [[UIImpactFeedbackGenerator alloc]
                            initWithStyle:UIImpactFeedbackStyleMedium];
        gHeavy        = [[UIImpactFeedbackGenerator alloc]
                            initWithStyle:UIImpactFeedbackStyleHeavy];

        // Rigid and Soft only exist from iOS 13. On anything older they stand in as the
        // nearest older style rather than going silent — a sharp tap is closer to Heavy
        // and a cushioned one closer to Light.
        if (@available(iOS 13.0, *)) {
            gRigid = [[UIImpactFeedbackGenerator alloc]
                         initWithStyle:UIImpactFeedbackStyleRigid];
            gSoft  = [[UIImpactFeedbackGenerator alloc]
                         initWithStyle:UIImpactFeedbackStyleSoft];
        } else {
            gRigid = gHeavy;
            gSoft  = gLight;
        }
    }
}

static void FeelHapticsImpact(UIImpactFeedbackGenerator* generator)
{
    if (generator == nil) return;

    if (@available(iOS 10.0, *)) {
        // prepare() before, not after: it warms the engine so the tap lands on the frame
        // it was asked for instead of a beat later.
        [generator prepare];
        [generator impactOccurred];
    }
}

extern "C" {

void FeelHapticsPlay(int pattern)
{
    if (pattern == FeelHapticNone) return;

    if (@available(iOS 10.0, *)) {
        FeelHapticsPrepare();

        switch ((FeelHapticPattern)pattern) {
            case FeelHapticSelection:
                [gSelection prepare];
                [gSelection selectionChanged];
                break;

            case FeelHapticLightImpact:  FeelHapticsImpact(gLight);  break;
            case FeelHapticMediumImpact: FeelHapticsImpact(gMedium); break;
            case FeelHapticHeavyImpact:  FeelHapticsImpact(gHeavy);  break;
            case FeelHapticRigidImpact:  FeelHapticsImpact(gRigid);  break;
            case FeelHapticSoftImpact:   FeelHapticsImpact(gSoft);   break;

            case FeelHapticSuccess:
                [gNotification prepare];
                [gNotification notificationOccurred:UINotificationFeedbackTypeSuccess];
                break;

            case FeelHapticWarning:
                [gNotification prepare];
                [gNotification notificationOccurred:UINotificationFeedbackTypeWarning];
                break;

            case FeelHapticFailure:
                [gNotification prepare];
                [gNotification notificationOccurred:UINotificationFeedbackTypeError];
                break;

            default:
                break;
        }
    }
}

}
