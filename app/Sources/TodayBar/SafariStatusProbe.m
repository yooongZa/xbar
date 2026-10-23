#import "SafariStatusProbe.h"
#import <SafariServices/SafariServices.h>
@implementation SafariStatusProbe
+ (void)check:(NSString *)identifier completion:(void (^)(BOOL))completion {
    // Some Safari versions reply on ExtensionHelper's XPC queue despite the
    // SDK's MainActor annotation. Hop before crossing into Swift UI code.
    [SFSafariExtensionManager getStateOfSafariExtensionWithIdentifier:identifier completionHandler:^(SFSafariExtensionState *state, NSError *error) {
        BOOL enabled = state.isEnabled;
        dispatch_async(dispatch_get_main_queue(), ^{ completion(enabled); });
    }];
}
+ (void)preferences:(NSString *)identifier completion:(void (^)(BOOL))completion {
    [SFSafariApplication showPreferencesForExtensionWithIdentifier:identifier completionHandler:^(NSError *error) {
        BOOL success = error == nil;
        dispatch_async(dispatch_get_main_queue(), ^{ completion(success); });
    }];
}
@end
