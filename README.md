# Intake Pipeline

This project can be used to set up file processing pipelines for files that will eventually be imported into a database, data lake, etc.

## Pipeline steps

Pipeline steps can be run independently and can be excuted in any order in a pipeline.

| Step      | Description                                                                             |
| --------- | --------------------------------------------------------------------------------------- |
| Ingest    | Move files for a source folder to a destination folder                                  |
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