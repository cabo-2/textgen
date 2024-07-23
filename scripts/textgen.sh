#!/bin/bash

# Define the options for getopt
options="i:o:hmv:ls:S:f:c:"

# Parse the command-line arguments
while getopts "$options" opt; do
  case $opt in
    i)
      input_dir="$OPTARG"
      ;;
    o)
      output_dir="$OPTARG"
      ;;
    m)
      model="$OPTARG"
      ;;
    v)
      textgen --version
      exit 0
      ;;
    l)
      textgen --list-model
      exit 0
      ;;
    s)
      system_prompt="$OPTARG"
      ;;
    S)
      system_file="$OPTARG"
      ;;
    f)
      format="$OPTARG"
      ;;
    c)
      config="$OPTARG"
      ;;
    h)
      textgen --help
      exit 0
      ;;
    \?)
      echo "Invalid option: -$OPTARG" >&2
      exit 1
      ;;
    :)
      echo "Option -$OPTARG requires an argument." >&2
      exit 1
      ;;
  esac
done

# Check if both input and output directories are specified
if [[ -z "$input_dir" || -z "$output_dir" ]]; then
  echo "Error: Both input and output directories must be specified."
  exit 1
fi

# Get the full path of the input directory
input_dir_full=$(readlink -f "$input_dir")

# Get the full path of the output directory
output_dir_full=$(readlink -f "$output_dir")

# Check if the input directory and output directory are the same
if [[ "$input_dir_full" == "$output_dir_full" ]]; then
  echo "Error: Input directory and output directory cannot be the same."
  exit 1
fi

# Get the remaining arguments
shift $((OPTIND - 1))
args=("$@")

# Find files in the input directory
find "$input_dir_full" -maxdepth 1 -type f -print0 | while IFS= read -r -d $'\0' file; do
  # Get the full path of the file
  file_full=$(readlink -f "$file")

  # Get the filename
  filename=$(basename "$file")

  # Create the full path of the output file
  output_file="$output_dir_full/$filename"

  # Execute textgen command with the full paths of input and output files
  textgen -P "$file_full" -o "$output_file" \
    ${model:+-m "$model"} \
    ${system_prompt:+-s "$system_prompt"} \
    ${system_file:+-S "$system_file"} \
    ${format:+-f "$format"} \
    ${config:+-c "$config"} \
    "${args[@]}"
done