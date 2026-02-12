//
//  AppstackUnityBridge.mm
//  Appstack Unity SDK - C exports that call Swift bridge
//

#import "AppstackUnityBridge.h"
#import <Foundation/Foundation.h>

#if __has_include("UnityFramework-Swift.h")
#import "UnityFramework-Swift.h"
#elif __has_include("Unity-iPhone-Swift.h")
#import "Unity-iPhone-Swift.h"
#else
#import "Unity-Swift.h"
#endif

static void _AppstackUnity_GetAttributionParamsCompletion(const char* paramsJson, const char* error);

void _AppstackUnity_Configure(const char* apiKey, bool isDebug, const char* endpointBaseUrl, int logLevel, const char* customerUserId) {
    NSString* apiKeyStr = apiKey ? [NSString stringWithUTF8String:apiKey] : @"";
    NSString* endpointStr = endpointBaseUrl && strlen(endpointBaseUrl) > 0 ? [NSString stringWithUTF8String:endpointBaseUrl] : nil;
    NSString* customerStr = customerUserId && strlen(customerUserId) > 0 ? [NSString stringWithUTF8String:customerUserId] : nil;
    [AppstackUnityBridge configureWithApiKey:apiKeyStr isDebug:isDebug endpointBaseUrl:endpointStr logLevel:logLevel customerUserId:customerStr];
}

void _AppstackUnity_SendEvent(const char* eventType, const char* eventName, const char* parametersJson) {
    NSString* eventTypeStr = eventType && strlen(eventType) > 0 ? [NSString stringWithUTF8String:eventType] : nil;
    NSString* eventNameStr = eventName && strlen(eventName) > 0 ? [NSString stringWithUTF8String:eventName] : nil;
    NSDictionary* params = nil;
    if (parametersJson && strlen(parametersJson) > 0) {
        NSData* data = [[NSString stringWithUTF8String:parametersJson] dataUsingEncoding:NSUTF8StringEncoding];
        if (data) {
            NSError* err = nil;
            params = [NSJSONSerialization JSONObjectWithData:data options:0 error:&err];
            if (!params || ![params isKindOfClass:[NSDictionary class]]) params = nil;
        }
    }
    [AppstackUnityBridge sendEvent:eventTypeStr eventName:eventNameStr parameters:params];
}

void _AppstackUnity_EnableAppleAdsAttribution(void) {
    [AppstackUnityBridge enableAppleAdsAttribution];
}

const char* _AppstackUnity_GetAppstackId(void) {
    NSString* id = [AppstackUnityBridge getAppstackId];
    if (!id || id.length == 0) return NULL;
    return strdup([id UTF8String]);
}

bool _AppstackUnity_IsSdkDisabled(void) {
    return [AppstackUnityBridge isSdkDisabled];
}

static void (*_g_attributionCompletion)(const char*, const char*) = NULL;

void _AppstackUnity_GetAttributionParams(void (*completion)(const char* paramsJson, const char* error)) {
    _g_attributionCompletion = completion;
    [AppstackUnityBridge getAttributionParamsWithCompletion:^(NSDictionary * _Nullable params, NSError * _Nullable error) {
        const char* paramsC = NULL;
        const char* errorC = NULL;
        if (error) {
            errorC = strdup([error.localizedDescription UTF8String]);
        } else if (params && [params isKindOfClass:[NSDictionary class]]) {
            NSError* err = nil;
            NSData* data = [NSJSONSerialization dataWithJSONObject:params options:0 error:&err];
            if (data) {
                NSString* json = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
                paramsC = strdup([json UTF8String]);
            }
        }
        if (!paramsC && !errorC) paramsC = strdup("{}");
        if (_g_attributionCompletion) {
            _g_attributionCompletion(paramsC ?: "", errorC ?: "");
            _g_attributionCompletion = NULL;
        }
        if (paramsC) free((void*)paramsC);
        if (errorC) free((void*)errorC);
    }];
}
