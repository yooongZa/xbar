#import <Foundation/Foundation.h>
NS_ASSUME_NONNULL_BEGIN
@interface SafariStatusProbe : NSObject
+ (void)check:(NSString *)identifier completion:(void (^)(BOOL enabled))completion;
+ (void)preferences:(NSString *)identifier completion:(void (^)(BOOL success))completion;
@end
NS_ASSUME_NONNULL_END
