//
//  AppstackUnityBridge.h
//  Appstack Unity SDK - C exports for Unity DllImport
//

#import <Foundation/Foundation.h>

#ifdef __cplusplus
extern "C" {
#endif

void _AppstackUnity_Configure(const char* apiKey, bool isDebug, const char* endpointBaseUrl, int logLevel, const char* customerUserId);
void _AppstackUnity_SendEvent(const char* eventType, const char* eventName, const char* parametersJson);
void _AppstackUnity_EnableAppleAdsAttribution();
const char* _AppstackUnity_GetAppstackId(void);
bool _AppstackUnity_IsSdkDisabled(void);
void _AppstackUnity_GetAttributionParams(void (*completion)(const char* paramsJson, const char* error));

#ifdef __cplusplus
}
#endif
