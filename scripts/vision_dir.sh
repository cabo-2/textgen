#!/bin/bash

usage() {
  cat <<'EOF'
Usage: vision_dir.sh [options]

Options:
  -m|--model <MODEL>                 Vision-capable model (e.g., gpt-4o, gpt-4o-mini).
  -i|--image-dir <DIR_PATH>          Directory containing images (non-recursive).
  -o|--output-dir <DIR_PATH>         Directory to save per-image outputs.
  -p|--prompt <STRING>               User prompt to pair with each image.
  -P|--prompt-file <FNAME>           User prompt from file.
  -s|--system <STRING>               System prompt string.
  -S|--system-file <FNAME>           System prompt from file.
  -f|--format <text|json>            Output format. Default: text.
  -c|--config <FNAME>                Parameter settings file (text, json).
  -l|--conversation-log <FNAME>      File to read and maintain conversation logs.
  -d|--debug <FNAME>                 Real-time API logging.
  --output-ext <EXT>                 Output extension (default: txt).
  -h|--help                          Show help information.
EOF
}

output_ext="txt"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -i|--image-dir)
      image_dir="$2"; shift 2 ;;
    -o|--output-dir)
      output_dir="$2"; shift 2 ;;
    -m|--model)
      model="$2"; shift 2 ;;
    -p|--prompt)
      prompt="$2"; shift 2 ;;
    -P|--prompt-file)
      prompt_file="$2"; shift 2 ;;
    -s|--system)
      system="$2"; shift 2 ;;
    -S|--system-file)
      system_file="$2"; shift 2 ;;
    -f|--format)
      format="$2"; shift 2 ;;
    -c|--config)
      config="$2"; shift 2 ;;
    -l|--conversation-log)
      log="$2"; shift 2 ;;
    -d|--debug)
      debug="$2"; shift 2 ;;
    --output-ext)
      output_ext="$2"; shift 2 ;;
    -h|--help)
      usage; exit 0 ;;
    *)
      echo "Invalid option: $1" >&2
      usage
      exit 1 ;;
  esac
done

# Validation
if [[ -z "$image_dir" || -z "$output_dir" || -z "$model" ]]; then
  echo "Error: Options -m/--model, -i/--image-dir, and -o/--output-dir are required." >&2
  usage
  exit 1
fi

if [[ -n "$prompt" && -n "$prompt_file" ]]; then
  echo "Error: Use only one of -p/--prompt or -P/--prompt-file." >&2
  exit 1
fi

if [[ -n "$system" && -n "$system_file" ]]; then
  echo "Error: Use only one of -s/--system or -S/--system-file." >&2
  exit 1
fi

image_dir_full=$(readlink -f "$image_dir") || exit 1
output_dir_full=$(readlink -f "$output_dir") || exit 1

if [[ "$image_dir_full" == "$output_dir_full" ]]; then
  echo "Error: Image directory and output directory cannot be the same." >&2
  exit 1
fi

if [[ ! -d "$image_dir_full" ]]; then
  echo "Error: Image directory does not exist: $image_dir_full" >&2
  exit 1
fi

mkdir -p "$output_dir_full"

found_any=false
allowed_exts="jpg jpeg png gif webp bmp tiff heic"

while IFS= read -r -d '' file; do
  filename=$(basename "$file")
  lower_name=${filename,,}
  ext="${lower_name##*.}"

  if ! grep -qw "$ext" <<<"$allowed_exts"; then
    echo "Skipping non-image file: $filename" >&2
    continue
  fi

  found_any=true

  base="${filename%.*}"
  output_file="$output_dir_full/$base.${output_ext}"

  if [[ -f "$output_file" ]]; then
    echo "Skipping $output_file as it already exists."
    continue
  fi

  image_full=$(readlink -f "$file")

  echo "Processing $filename -> $output_file"
  if ! textgen -a "$image_full" -o "$output_file" \
      -m "$model" \
      ${prompt:+-p "$prompt"} \
      ${prompt_file:+-P "$prompt_file"} \
      ${system:+-s "$system"} \
      ${system_file:+-S "$system_file"} \
      ${format:+-f "$format"} \
      ${config:+-c "$config"} \
      ${log:+-l "$log"} \
      ${debug:+-d "$debug"}; then
    echo "Error: textgen failed for $filename" >&2
    exit 1
  fi
done < <(find "$image_dir_full" -maxdepth 1 -type f -print0)

if [[ "$found_any" = false ]]; then
  echo "Error: No files found in $image_dir_full" >&2
  exit 1
fi
