# Research result: the project is materially dated

As of **July 20, 2026**, the serious open-weight image-to-video field has moved well beyond Mochi 1 and CogVideoX. For your **RTX 5090 32GB / 64GB RAM workstation**, I would build orient-express around **LTX-2.3, Wan 2.2, and HunyuanVideo 1.5**, then add specialist models for editing, reference consistency, and long-form continuation.

Independent blind-vote benchmarking currently places **LTX-2.3 Pro/Fast first among open-weight I2V models that generate audio**, while **NVIDIA Cosmos3-Super-I2V** leads open-weight I2V without audio. These scores are useful signals, not objective proof that one model wins every workflow. ([Artificial Analysis][1])

## Recommended model list

| Model                      | Best role in orient-express                                                                                                                              |                                                                        Local hardware fit | License / concern                                                                                              |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------: | -------------------------------------------------------------------------------------------------------------- |
| **LTX-2.3**                | Flagship final-render model; synchronized audio/video, cinematic shots, keyframe interpolation, retakes, camera-control LoRAs, lip dub and HDR workflows |      **Good fit for 5090 32GB** using distilled checkpoints, FP8 and selective offloading | Custom **LTX-2 Community License**; companies with at least US$10M annual revenue require a commercial license |
| **Wan 2.2 TI2V-5B**        | Permissive general-purpose I2V/T2V renderer; reliable 720p production baseline                                                                           |                                **Excellent fit**; official pipeline targets at least 24GB | **Apache 2.0**                                                                                                 |
| **HunyuanVideo 1.5**       | Efficient high-quality renderer; strong lower-VRAM fallback and LoRA base                                                                                |                 **Excellent fit**; official minimum is approximately 14GB with offloading | Custom Tencent license; unavailable under that license in the EU, UK and South Korea                           |
| **SkyReels V3**            | Multi-reference generation using people, objects and background references; shot extension and human-centric workflows                                   | **Good fit**; official low-VRAM mode supports under 24GB through FP8 and block offloading | Custom Skywork Community License                                                                               |
| **LongCat-Video**          | Long video continuation and multi-segment generation                                                                                                     |                            **Likely workable**, but benchmark it before promising support | **MIT**                                                                                                        |
| **VACE / Wan2.1-VACE-14B** | Editing and control layer: masked changes, object movement, replacement, expansion and reference-driven animation                                        |                                       Heavy but practical on 32GB with optimized runtimes | **Apache 2.0** for the Wan-based weights                                                                       |
| **Cosmos3-Super-I2V**      | Maximum-quality optional render server, physical coherence, synthetic-world footage                                                                      |           **Not a normal 5090 backend**; 64B model aimed at H200/B200/GB200-class systems | NVIDIA **OpenMDW 1.1**                                                                                         |

### 1. LTX-2.3 — best flagship for your application

This should be the primary “quality” backend.

LTX-2.3 has a 22B development checkpoint and distilled checkpoint, synchronized audio generation, spatial and temporal upscalers, FP8 execution, image-to-video, keyframe interpolation, retake/regeneration of selected time regions, audio-to-video, HDR workflows, lip dubbing, pose control, motion tracking and camera-control LoRAs. Its official two-stage pipeline is specifically presented as the production-quality route. ([GitHub][2])

Its greatest advantages for your editor are not simply visual quality:

* Native synchronized sound generation
* Regeneration of only a damaged section
* Keyframe-to-keyframe interpolation
* Shot-level camera controls
* Image/video transformation through IC-LoRA
* LoRA and full training support

The distilled pipeline uses an 8-step first stage and 4-step second stage, while FP8 and CPU/disk offloading are supported for constrained memory. That makes your 32GB Blackwell GPU a sensible target, although the highest-quality multi-stage workflows remain computationally heavy. ([GitHub][2])

**Major issue:** it is not Apache 2.0. The current LTX-2 Community License requires entities earning at least US$10 million annually to obtain a paid commercial license. ([GitHub][3])

### 2. Wan 2.2 — best permissive production base

Wan 2.2 is the most important model to include when you want a broadly distributable, permissively licensed backend.

The relevant versions are:

* **Wan2.2-TI2V-5B:** unified text/image-to-video, 720p at 24 fps, officially runs with 24GB VRAM.
* **Wan2.2-I2V-A14B:** higher-capacity 480p/720p image-to-video model.
* **Wan2.2-Animate-14B:** character animation and character replacement.
* **Wan2.2-S2V-14B:** audio/speech-driven video.

The official implementation requires approximately **80GB VRAM** for the full A14B single-GPU path, so your application should not pretend that A14B is a native 5090 model. Community quantization and aggressive offloading can reduce that, but those should be separately labeled compatibility profiles. The 5B version is the correct default for a 24–32GB local machine. ([GitHub][4])

Wan also allows prompt extension to be skipped entirely or performed using a local Qwen model, which matters for a verifiably offline workflow. ([GitHub][4])

The weights are Apache 2.0, although the project still imposes responsibility for unlawful, harmful, privacy-violating or misinformation-related uses. ([GitHub][4])

### 3. HunyuanVideo 1.5 — best efficiency/quality balance

HunyuanVideo 1.5 is only **8.3B parameters**, supports image-to-video, has official Diffusers integration, training code and LoRA tuning, and is designed for consumer GPUs. ([GitHub][5])

The 480p step-distilled I2V checkpoint runs in 8 or 12 steps. Tencent reports a generation time below 75 seconds on an RTX 4090 with roughly 75% acceleration compared with the original pipeline; treat this as a vendor benchmark until independently tested in your application. ([GitHub][5])

This is a strong option for:

* Preview rendering
* Lower-VRAM installations
* Batch generation
* LoRA experimentation
* Systems where LTX-2.3 is too heavy

The license is the problem. It expressly excludes the **European Union, United Kingdom and South Korea**. That does not prevent your own Canadian use, but it prevents orient-express from presenting Hunyuan as a universally distributable built-in model. ([GitHub][6])

### 4. SkyReels V3 — best multi-reference specialist

SkyReels V3 is more valuable as a specialist than as your universal renderer. Its reference-to-video model accepts **one to four reference images**, including separate characters, objects and backgrounds, and attempts to preserve their compositional relationships across the shot. ([GitHub][7])

It is useful for:

* Recurring illustrated subjects
* Reenactments with consistent wardrobe and locations
* Maintaining a specific object across a scene
* Multi-person compositions
* Video extension and shot changes
* Audio-driven human video

Its low-VRAM mode uses FP8 weight-only quantization and block offloading and is explicitly intended for GPUs below 24GB. ([GitHub][7])

It uses the custom Skywork Community License rather than Apache or MIT. ([GitHub][8])

### 5. LongCat-Video — best long-form research candidate

LongCat-Video is a **13.6B unified T2V/I2V/video-continuation model**. Its differentiator is that video continuation was part of native pretraining rather than added as an afterthought. The authors claim minute-scale 720p/30fps generation without progressive colour or quality drift, using coarse-to-fine generation and block-sparse attention. Those long-duration claims are vendor-reported and deserve your own testing. ([GitHub][9])

It is especially interesting for:

* Extending B-roll
* Slowly evolving establishing shots
* Background plates
* Long atmospheric sequences
* Multi-segment generation

The weights and repository are MIT licensed, making it unusually attractive from a distribution standpoint. ([GitHub][9])

### 6. VACE — essential editing layer

VACE is not the newest raw-quality champion. It is valuable because it maps directly onto an editor.

Its supported concepts include:

* Move Anything
* Swap Anything
* Reference Anything
* Expand Anything
* Animate Anything
* Masked video editing
* Video-to-video transformation
* Reference-to-video generation

The Wan2.1-VACE-14B checkpoint targets approximately 81 frames at 1280×720 and inherits Apache 2.0. ([GitHub][10])

For orient-express, this should power operations such as:

```text
Replace selected region
Extend shot
Repair selected frames
Animate selected object
Change background
Apply reference appearance
Generate into timeline gap
```

### 7. NVIDIA Cosmos3-Super-I2V — benchmark leader, wrong default hardware

Cosmos3-Super-I2V currently leads Artificial Analysis’s open-weight no-audio I2V ranking. However, it is a **64B model**. NVIDIA recommends H200, B200 or GB200-class infrastructure for Cosmos3-Super, and an example serving configuration uses four GPUs. ([Artificial Analysis][1])

Therefore:

* Include it as an optional **datacenter/on-prem render provider**.
* Do not include it in the default 5090 installation.
* Do not let its benchmark position distort the desktop architecture.

It is released under OpenMDW 1.1 rather than Apache 2.0. ([Hugging Face][11])

## The stack I would ship

### Core pack

```text
LTX-2.3 Distilled
  Final-quality audiovisual generation
  Retake, keyframes, camera control, audio

Wan 2.2 TI2V-5B
  Permissive general renderer
  720p / 24 fps
  Reliable 24–32GB deployment target

HunyuanVideo 1.5 Distilled
  Fast previews and efficient I2V
  LoRA experimentation
```

### Production extensions

```text
VACE
  Masked editing, replacement, extension and repair

SkyReels V3
  Multi-reference identity, object and scene consistency

LongCat-Video
  Long continuation and atmospheric footage
```

### Optional infrastructure pack

```text
Cosmos3-Super-I2V
  Multi-GPU quality backend
  Disabled unless compatible hardware is detected
```

## Models to remove or demote

### Mochi 1

Remove it from the I2V table. Mochi 1 was a major open release in 2024, but the released preview was fundamentally **text-to-video**, not a proper modern image-to-video backend. ([GitHub][12])

### CogVideoX

CogVideoX1.5-I2V can remain as a lightweight legacy compatibility backend, but it should no longer be described as an industry-leading option. Its I2V generation originated in the 2024 model line. ([GitHub][13])

### Generic “LTX-2”

Pin the exact supported checkpoint:

```text
LTX-2.3-22B-Distilled-1.1
LTX-2.3-22B-Dev
```

Do not let the application silently resolve “latest,” because pipelines, LoRAs and upscalers are checkpoint-specific.

## Important corrections to the current project claims

### Do not license the entire bundle as Apache 2.0

Your application code can be Apache 2.0. The downloaded models cannot all inherit that license.

Use a structure such as:

```text
LICENSE                    # orient-express application license
THIRD_PARTY_NOTICES.md
licenses/
├── ltx-2-community.txt
├── wan2.2-apache-2.0.txt
├── hunyuan-community.txt
├── skywork-community.txt
├── longcat-mit.txt
├── vace-apache-2.0.txt
└── cosmos-openmdw-1.1.txt
```

LTX Desktop follows this same separation: its application is Apache 2.0, while model weights are downloaded separately and remain subject to their own terms. ([GitHub][14])

### Replace “no content restrictions”

That statement is not technically or legally accurate. Several models have acceptable-use restrictions, custom commercial terms or geographic restrictions.

A defensible description is:

> **No remote generation service or service-side prompt gatekeeping. Model execution occurs locally, subject to the selected model’s license and applicable law.**

### Remove the model-origin censorship column

“China,” “Israel” or “USA” is not a reliable technical indicator of whether a local checkpoint contains filtering.

Track these instead:

```text
license
weights_available
offline_prompting
network_required
external_prompt_enhancer
safety_checker
safety_checker_configurable
telemetry_audited
supported_runtimes
minimum_vram
tested_checkpoint_hash
```

Wan, for example, can skip prompt extension entirely or run it through a local model. That is a measurable property; national origin is not. ([GitHub][4])

### Do not promise universal native macOS parity

Current high-end video generation remains overwhelmingly CUDA-first. Even LTX’s official desktop application supports local generation on Windows/Linux NVIDIA hardware while placing macOS in API mode. ([GitHub][14])

For your M3 Max, I would use:

```text
macOS:
  Full editor and project management
  Media ingestion
  Timeline cutting
  Prompting
  Job submission
  Result review

Windows/Linux NVIDIA node:
  Model execution
  LoRA training
  Upscaling
  Frame interpolation
  Batch rendering
```

The render node can remain entirely inside your local network. That still satisfies a private/offline workflow without making a false hardware-parity claim.

## Recommended application architecture

Do not hardwire orient-express directly into ComfyUI graphs. Keep ComfyUI as an optional workflow backend and research environment.

Use a stable model interface:

```python
class VideoModelAdapter:
    def capabilities(self) -> ModelCapabilities: ...
    def validate(self, job: GenerationJob) -> ValidationResult: ...
    def estimate_resources(self, job: GenerationJob) -> ResourceEstimate: ...
    def generate(self, job: GenerationJob) -> GenerationResult: ...
    def cancel(self, job_id: str) -> None: ...
```

Each model should run in its own worker environment because their CUDA, Torch, Diffusers and custom-kernel requirements will conflict.

```text
orient-express/
├── app/
├── timeline/
├── render_queue/
├── model_registry/
├── adapters/
│   ├── ltx23/
│   ├── wan22/
│   ├── hunyuan15/
│   ├── skyreels_v3/
│   ├── longcat/
│   ├── vace/
│   └── cosmos3/
├── workers/
├── security/
├── provenance/
├── workflows/
└── ffmpeg/
```

Every generated clip should receive a sidecar manifest containing:

```json
{
  "source_asset_hashes": [],
  "model_id": "",
  "checkpoint_sha256": "",
  "adapter_version": "",
  "seed": 0,
  "prompt": "",
  "negative_prompt": "",
  "generation_parameters": {},
  "created_at": "",
  "synthetic_media": true
}
```

That is especially important for investigative journalism: original source material, edited evidence and synthetic illustrative footage should remain cryptographically and visually distinguishable.

**My final priority order for your 5090 system is:**

1. **LTX-2.3**
2. **Wan 2.2 TI2V-5B**
3. **HunyuanVideo 1.5**
4. **VACE**
5. **SkyReels V3**
6. **LongCat-Video**
7. **Cosmos3-Super-I2V as an external heavyweight backend**

This field changes quickly; a monthly watch for new open-weight I2V releases, license changes and 32GB performance improvements would be useful. Want me to set that up?

[1]: https://artificialanalysis.ai/video/leaderboard/image-to-video "https://artificialanalysis.ai/video/leaderboard/image-to-video"
[2]: https://github.com/Lightricks/LTX-2 "https://github.com/Lightricks/LTX-2"
[3]: https://github.com/Lightricks/LTX-2/blob/main/LICENSE "https://github.com/Lightricks/LTX-2/blob/main/LICENSE"
[4]: https://github.com/Wan-Video/Wan2.2 "https://github.com/Wan-Video/Wan2.2"
[5]: https://github.com/Tencent-Hunyuan/HunyuanVideo-1.5 "https://github.com/Tencent-Hunyuan/HunyuanVideo-1.5"
[6]: https://github.com/Tencent-Hunyuan/HunyuanVideo-1.5/blob/main/LICENSE "https://github.com/Tencent-Hunyuan/HunyuanVideo-1.5/blob/main/LICENSE"
[7]: https://github.com/SkyworkAI/SkyReels-V3 "https://github.com/SkyworkAI/SkyReels-V3"
[8]: https://github.com/SkyworkAI/SkyReels-V3/blob/main/LICENSE.txt "https://github.com/SkyworkAI/SkyReels-V3/blob/main/LICENSE.txt"
[9]: https://github.com/meituan-longcat/LongCat-Video "https://github.com/meituan-longcat/LongCat-Video"
[10]: https://github.com/ali-vilab/VACE "https://github.com/ali-vilab/VACE"
[11]: https://huggingface.co/nvidia/Cosmos3-Super "https://huggingface.co/nvidia/Cosmos3-Super"
[12]: https://github.com/genmoai/mochi "https://github.com/genmoai/mochi"
[13]: https://github.com/zai-org/CogVideo "https://github.com/zai-org/CogVideo"
[14]: https://github.com/Lightricks/LTX-Desktop "https://github.com/Lightricks/LTX-Desktop"

---

## The next step is **not more broad research**

We now know which model families matter. Continuing to add models without an integration standard would produce a large catalogue but not a buildable application.

The next documentation phase should convert the research into an **authoritative technical specification** for orient-express.

## Phase 2 — System Definition and Model Integration Specification

The first document I would create is:

```text
docs/architecture/orient-express-SYSTEM-SPEC.md
```

It should define what the application actually is in 2026:

> A local-first, model-agnostic video generation and editing platform that routes image-to-video jobs to isolated model workers according to capability, hardware, licensing and editorial requirements.

This replaces the old assumption that orient-express is primarily “ComfyUI plus several downloaded checkpoints.”

## 1. Lock the product architecture

Document these layers:

```text
Desktop application
    ↓
Local orchestration API
    ↓
Job planner and capability router
    ↓
Isolated model workers
    ├── LTX-2.3
    ├── Wan 2.2
    ├── VACE
    ├── LongCat
    ├── SkyReels
    └── Hunyuan
    ↓
Post-processing pipeline
    ↓
Asset store, provenance records and export
```

ComfyUI should be treated as:

* A supported workflow runtime
* A visual debugging and prototyping interface
* An optional execution adapter

It should **not** be the central architecture or permanent public API.

## 2. Define model roles instead of one model ranking

Create:

```text
docs/models/MODEL-ROLES.md
```

The initial role map should be:

| Role                            | Primary          | Secondary                      |
| ------------------------------- | ---------------- | ------------------------------ |
| General high-quality generation | LTX-2.3          | Wan 2.2 TI2V-5B                |
| Fast drafts and iteration       | Wan 2.2 TI2V-5B  | HunyuanVideo 1.5               |
| Source-image fidelity           | Wan 2.2 I2V-A14B | SkyReels V3                    |
| Surgical retakes                | LTX-2.3          | VACE                           |
| Masked editing and replacement  | VACE             | LTX IC-LoRA                    |
| Multi-reference consistency     | SkyReels V3      | VACE                           |
| Long continuation               | LongCat-Video    | SkyReels extension             |
| Native synchronized audio       | LTX-2.3          | Specialist audio-driven models |
| Heavy private render server     | Cosmos3-Super    | Wan A14B multi-GPU             |

This prevents the UI from asking users to understand model families. The user selects a task such as **“Extend Shot”** or **“Preserve Subject,”** and the router recommends the correct worker.

## 3. Specify the universal generation contract

Create:

```text
docs/architecture/MODEL-ADAPTER-SPEC.md
```

Every worker should implement the same conceptual interface:

```python
class VideoModelAdapter:
    def inspect_capabilities(self) -> ModelCapabilities: ...
    def validate_job(self, job: GenerationJob) -> ValidationResult: ...
    def estimate_job(self, job: GenerationJob) -> ResourceEstimate: ...
    def prepare_job(self, job: GenerationJob) -> PreparedJob: ...
    def generate(self, job: PreparedJob) -> GenerationResult: ...
    def cancel(self, job_id: str) -> None: ...
    def collect_diagnostics(self, job_id: str) -> Diagnostics: ...
```

The capability declaration must cover:

```text
image_to_video
reference_to_video
video_to_video
masked_editing
first_frame_conditioning
last_frame_conditioning
multiple_keyframes
video_continuation
camera_control
motion_control
audio_generation
audio_conditioning
lip_sync
retake
lora
maximum_frames
native_resolution
supported_precision
estimated_vram
```

This is the most important architectural document because it prevents every model integration from becoming bespoke application code.

## 4. Build the model registry specification

Create:

```text
docs/models/MODEL-REGISTRY-SPEC.md
```

Each exact checkpoint needs a machine-readable record:

```yaml
id: ltx-2.3-22b-distilled-1.1
family: ltx
version: "2.3"
task_support:
  image_to_video: true
  video_to_video: true
  retake: true
  synchronized_audio: true

artifacts:
  - filename: ltx-2.3-22b-distilled-1.1.safetensors
    sha256: REQUIRED

runtime:
  adapter: ltx23
  precision:
    - bf16
    - fp8
  minimum_vram_gb: 24
  recommended_vram_gb: 32

license:
  id: ltx-2-community
  redistributable: false
  download_separately: true
  commercial_revenue_threshold_usd: 10000000

security:
  network_required_after_install: false
  prompt_rewriter_default: false
  telemetry_present: false
```

The registry—not README prose—should become the source of truth.

## 5. Establish an internal benchmark methodology

This is where limited mathematics becomes valuable.

Create:

```text
docs/benchmarks/I2V-BENCHMARK-PROTOCOL.md
```

Do not rely entirely on vendor demos or public Elo rankings. Build a fixed internal evaluation suite using identical source images and prompt families.

### Evaluation categories

```text
Source-image preservation
Prompt adherence
Motion coherence
Temporal stability
Human anatomy
Object permanence
Identity preservation
Camera obedience
Background stability
Editing precision
Generation speed
Peak VRAM
Deterministic repeatability
Failure rate
```

Use a weighted score:

```math
[
S_m =
w_fF_m +
w_pP_m +
w_tT_m +
w_iI_m +
w_cC_m +
w_eE_m +
w_rR_m
------
```

## w_vV_m

```math
w_lL_m
]
```

Where:

* (F): source fidelity
* (P): prompt adherence
* (T): temporal coherence
* (I): identity consistency
* (C): controllability
* (E): editing capability
* (R): runtime practicality
* (V): VRAM burden
* (L): licensing or deployment burden

But there should **not** be one universal score. Use task-specific profiles:

```text
General generation profile
Forensic reconstruction profile
Character consistency profile
Fast preview profile
Long continuation profile
Editing and repair profile
```

A model can therefore rank first for editing while ranking fourth for photorealistic generation.

## 6. Write the security threat model

Create:

```text
docs/security/THREAT-MODEL.md
```

The security documentation must address more than “disconnect the network.”

Threats include:

* Model installers downloading mutable files
* Compromised ComfyUI custom nodes
* Python package supply-chain attacks
* Model files containing unsafe serialized objects
* Prompt or asset leakage through optional APIs
* Telemetry embedded in desktop wrappers
* Hidden prompt rewriting
* Sensitive source material persisting in temporary folders
* Generated proxies leaking into thumbnails or caches
* Untrusted media exploiting FFmpeg or image decoders
* Workers accessing unrelated project directories

Required controls:

```text
Hash-pinned checkpoints
Locked dependency manifests
Safetensors-only policy where possible
No pickle checkpoint loading
Per-worker network controls
Read-only model mounts
Project-scoped filesystem access
Ephemeral worker scratch directories
Explicit prompt-rewriter disclosure
Audit logs without storing source content
Offline installation bundles
Signed model-registry releases
```

## 7. Define journalistic provenance

Create:

```text
docs/provenance/EDITORIAL-PROVENANCE-SPEC.md
```

The application needs to distinguish:

```text
Original evidence
Transcoded evidence
Editorially modified evidence
AI-assisted restoration
AI-generated illustration
AI-generated reconstruction
Fully synthetic footage
```

Every output should have:

* Source hashes
* Exact checkpoint hashes
* Original and transformed prompts
* Seeds and scheduler settings
* Reference assets
* Masks and conditioning inputs
* Generation date
* Software and worker versions
* Synthetic-media classification
* Human-readable disclosure recommendation

This is not censorship. It protects the credibility of the journalist using the system.

## 8. Rewrite the public-facing documentation last

Only after the technical specifications are stable should we replace:

```text
README.md
docs/INSTALLATION.md
docs/MODELS.md
docs/SECURITY.md
docs/TRAINING.md
```

The README should no longer promise:

* One universal Apache 2.0 licence
* Equivalent macOS and CUDA rendering
* “No restrictions” across every bundled model
* A fixed 100GB model download
* ComfyUI as the application itself

## Recommended documentation sequence

```text
01. orient-express-SYSTEM-SPEC.md
02. MODEL-ADAPTER-SPEC.md
03. MODEL-REGISTRY-SPEC.md
04. MODEL-ROLES.md
05. I2V-BENCHMARK-PROTOCOL.md
06. THREAT-MODEL.md
07. EDITORIAL-PROVENANCE-SPEC.md
08. WORKER-ISOLATION-SPEC.md
09. INSTALLATION-AND-HARDWARE-MATRIX.md
10. IMPLEMENTATION-ROADMAP.md
11. Rewrite README and user documentation
```

## My lead decision

We should proceed first with a **comprehensive system specification**, not additional theory or model collecting.

The model research answered:

> “What technology exists?”

The next document must answer:

> “What exactly are we building, how do its components interact, and what contracts prevent it from becoming an unstable collection of incompatible AI repositories?”

After that specification, the benchmark protocol becomes the next research-heavy phase. That is where we validate models directly on your RTX 5090 and replace estimated performance with reproducible orient-express data.
