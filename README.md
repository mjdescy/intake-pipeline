# Intake Pipeline

This project can be used to set up file processing pipelines for files that will eventually be imported into a database, data lake, etc.

## Pipeline steps

Pipeline steps can be run independently and can be excuted in any order in a pipeline.

| Step      | Description                                                                             |
| --------- | --------------------------------------------------------------------------------------- |
| Ingest    | Copy files from a source folder to a destination folder, recording hashes and sizes in a manifest |
| Unpack    | Extract files found within archive formats such as .zip                                 |
| Backup    | Create a backup of a group of files; optionally compress them into an archive           |
| Inspect   | Collect metadata, such as control totals, from files                                    |
| Classify  | Classify files according to what schema requirements they match                         |
| Transform | Convert data files to preferred formats (e.g., convert .xlsx to .csv)                   |
| Package   | Materialize a set of files listed on a manifest to a clean package in a standard format |
| Manifest  | Describe every file under a folder in a manifest, recording its path, name, SHA-256 hash, size, and extension |

## Data lineage

Data lineage will be tracked throughout the pipeline.

## Manifests

Files will be kept track of in manifests, which are file lists that describe the contents of a folder or package. In this context, a package is merely a group of files.

The manifest is a shared structure: its definition (`FileManifest`, `ManifestFileEntry`, `ManifestStatus`) and its JSON read/write code (`ManifestIO`) live in the `IntakePipeline.Core` class library, which every pipeline step references. Each step keeps only its own runner and CLI.

## Projects

| Project | Kind | Purpose |
| --- | --- | --- |
| `IntakePipeline.Core` | class library | Shared domain types, including the manifest |
| `IntakePipeline.Step.Ingest` | console app | Ingest step |
| `IntakePipeline.Step.Manifest` | console app | Manifest step |

## Ingest step

The Ingest step copies (never moves) every file under a source folder to a destination folder, preserving the folder structure, and records the outcome in a result manifest. It is a console app driven by an input manifest:

```json
{
  "sourceFolder": "data/incoming",
  "destinationFolder": "data/raw"
}
```

Relative paths resolve against the input manifest's own folder. Run it like this:

```sh
IntakePipeline.Step.Ingest ingest-manifest.json --output ingest-result.json
```

Options: `--output/-o` writes the result manifest to a file; `--json` prints it to stdout instead (wins over `--quiet`); `--quiet/-q` suppresses all stdout; `--version` prints the build version. Exit codes: 0 = success, 1 = usage error, 2 = runtime error.

The result manifest lists, for every file discovered under the source folder: its source and destination paths, its SHA-256 hash (lowercase hex), its size in bytes, and any per-file error; plus global errors and an overall status (`success`/`failure`).

## Manifest step

The Manifest step builds a manifest describing every file under a folder, including all subfolders. Each entry records the file's path, name, extension, size in bytes, and SHA-256 hash (lowercase hex). It reads files only; nothing is copied, moved, or changed. Run it like this:

```sh
IntakePipeline.Step.Manifest data/raw --run-id 6f9619ff-8b86-d011-b42d-00cf4fc964ff --output manifest.json
```

Options: `--run-id` is the GUID identifying the run; `--output/-o` writes the manifest to a file (required unless `--json` is used); `--json` prints it to stdout instead (wins over `--quiet`); `--quiet/-q` suppresses all stdout; `--version` prints the build version. Exit codes: 0 = success, 1 = usage error, 2 = runtime error.

The manifest records the run id, the manifested folder, the UTC date/time of the run start and completion, an overall status (`success`/`failure`), one entry per file, and any global errors:

```json
{
  "runId": "6f9619ff-8b86-d011-b42d-00cf4fc964ff",
  "folder": "/data/raw",
  "startedAtUtc": "2026-09-26T12:00:00.0000000+00:00",
  "completedAtUtc": "2026-09-26T12:00:01.0000000+00:00",
  "status": "success",
  "entries": [
    {
      "filePath": "/data/raw/a.txt",
      "fileName": "a.txt",
      "sha256": "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
      "fileSizeInBytes": 5,
      "fileExtension": ".txt",
      "error": null
    }
  ],
  "errors": []
}
```