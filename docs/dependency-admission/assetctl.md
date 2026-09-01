---
schema_version: '1.1'
id: 'reference-8xbeq2-assetctl-dependency-admission'
title: 'AssetCtl Dependency Admission'
description: 'Records ADR 0003 evidence for AssetCtl YAML, logging, raster, and SVG dependencies.'
doc_type: 'reference'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'agent'
tags:
  - 'dependencies'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/specs/asset-pipeline-tool.md'
confidence: 'high'
visibility: 'public'
license: null
---

# AssetCtl dependency admission

Evidence was refreshed from official package and provider documentation on 2026-09-01. The tool uses typed REST clients rather than vendor SDKs; provider endpoints, model IDs, capabilities, and economics remain configuration.

| Package | Current consumer and native alternative | Maintenance, compatibility, and license | Failure, coupling, and removal boundary |
| --- | --- | --- | --- |
| `YamlDotNet` 18.1.0 | Strict parsing of bounded tool-only YAML. A bespoke parser would duplicate mature scalar and syntax handling at a hostile-input boundary. | Current NuGet package; explicit `net10.0` asset; MIT; managed only. | Malformed or prohibited YAML fails before binding. Confined to `Configuration`; replace behind `StrictYaml`. |
| `Microsoft.Extensions.Logging` 10.0.11 | Constructor-injected logging contract required by ADR 0008. The BCL has no equivalent application abstraction. | Current .NET 10 Extensions family; MIT; managed only. | Sink failure is isolated from command results. Interfaces remain at composition and service constructors. |
| `Serilog` 4.4.0, `Serilog.Extensions.Logging` 10.0.0, console 6.1.1, file 7.0.0 | Required structured backend, stderr console, and bounded rolling local file. | Actively maintained; Apache-2.0 core/bridge and compatible sink packages; managed only. | Logging degrades to stderr without becoming authority. Replace solely in composition root. |
| `SkiaSharp` and Linux no-dependencies native assets 4.151.1 | Full PNG decode, pixel/alpha inspection, deterministic normalization, resizing, and PNG placeholder generation. BCL lacks an image codec. | Current matched packages; MIT; .NET 10 compatible; native `libSkiaSharp` requires compatible Linux libc/font libraries. | Native load/decode failures are explicit mechanical failures. Confined to local generation and validation; another codec can replace those adapters. |
| `Svg.Skia` 5.2.3 | Render sanitized SVG and target-size previews through the same Skia surface. XML APIs sanitize but cannot prove renderability. | Current NuGet package; MIT; managed wrapper over matched SkiaSharp. | Parse/render failure rejects the candidate. Confined to SVG validation and preview rendering. |

Package provenance: [YamlDotNet](https://www.nuget.org/packages/YamlDotNet/18.1.0), [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.11), [Serilog](https://www.nuget.org/packages/Serilog/4.4.0), [SkiaSharp](https://www.nuget.org/packages/SkiaSharp/4.151.1), [SkiaSharp upstream](https://github.com/mono/SkiaSharp), and [Svg.Skia](https://www.nuget.org/packages/Svg.Skia/5.2.3).

Provider contract evidence: [Recraft API](https://www.recraft.ai/docs/api-reference/endpoints), [OpenAI image generation](https://platform.openai.com/docs/guides/image-generation), [OpenAI vision](https://platform.openai.com/docs/guides/images-vision), [OpenAI structured outputs](https://platform.openai.com/docs/guides/structured-outputs), and [xAI Imagine](https://docs.x.ai/developers/model-capabilities/imagine).
