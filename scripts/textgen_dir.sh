#!/bin/bash

usage() {
  echo "Usage: textgen_dir.sh [options]"
  echo ""
  echo "Options:"
  echo "  -m|--model <MODEL>                Specify the model to use (gpt-4o, gpt-4o-mini)."
  echo "  -i|--input-prompt-dir <DIR_PATH>  Input message from directory."
  echo "  -o|--output-dir <DIR_PATH>        Directory to save output file."
  echo "  -s|--system-file <FNAME>          System prompt from a file."
  echo "  -f|--format <FORMAT>              Output format (text, json)"
  echo "                                    Allowed values are: text, json."
  echo "                                    Default value is: text."
  echo "  -c|--config <FNAME>               Parameter settings file (text, json)."
  echo "  -h|--help                         Show help information."
}

# Define the options for getopt
options="i:o:m:s:f:c:h"

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
    s)
      system_file="$OPTARG"
      ;;
    f)
      format="$OPTARG"
      ;;
    c)
      config="$OPTARG"
      ;;
    h)
      usage
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

# Check for mandatory options
if [[ -z "$input_dir" || -z "$output_dir" || -z "$model" || -z "$system_file" ]]; then
  echo "Error: Options -m, -i, -o, and -s are required."
  usage
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

# Find files in the input directory
find "$input_dir_full" -maxdepth 1 -type f -print0 | while IFS= read -r -d $'\0' file; do
  # Get the full path of the file
  file_full=$(readlink -f "$file")

  # Get the filename
  filename=$(basename "$file")

  # Create the full path of the output file
  output_file="$output_dir_full/$filename"

  # Check if the output file already exists
  if [[ -f "$output_file" ]]; then
    echo "Skipping $output_file as it already exists."
    continue
  fi

  # Execute textgen command with the full paths of input and output files
  textgen -P "$file_full" -o "$output_file" \
    -m "$model" \
    -S "$system_file" \
    ${format:+-f "$format"} \
    ${config:+-c "$config"}
done