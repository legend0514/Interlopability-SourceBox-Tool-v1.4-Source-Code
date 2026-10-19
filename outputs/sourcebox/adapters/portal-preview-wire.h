#pragma once
// Local module boundary, not a network packet. Caller checks this ABI version.
struct SourceboxPreviewEndpoint {float position[3],normal[3],right[3],up[3];};
struct SourceboxPreviewPair {SourceboxPreviewEndpoint entry,exit;float halfWidth,halfHeight;};
static constexpr int SOURCEBOX_PREVIEW_ABI=1;
typedef int (*SourceboxReadPreviewsFn)(int,SourceboxPreviewPair*,int);
