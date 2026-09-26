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

## Data lineage

Data lineage will be tracked throughout the pipeline.

## Manifests

Files will be kept track of in manifests, which are file lists that describe the contents of a folder or package. In this context, a package is merely a group of files.

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