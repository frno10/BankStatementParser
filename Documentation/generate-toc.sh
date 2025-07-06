#!/bin/bash
# Regenerate Documentation Table of Contents for BankStatementParsing
DOCS_DIR="$(dirname "$0")"
README="$DOCS_DIR/README.md"

echo "# Documentation Table of Contents" > "$README"
echo >> "$README"
echo "Welcome to the BankStatementParsing documentation hub. Use the links below to navigate the available documentation." >> "$README"
echo >> "$README"

find "$DOCS_DIR" -type f -name '*.md' ! -name 'README.md' | while read -r file; do
    relpath="${file#$DOCS_DIR/}"
    name="$(basename "$file" .md)"
    echo "- [${name}](${relpath})" >> "$README"
done
echo >> "$README"
echo '---' >> "$README"
echo >> "$README"
echo 'For additional details, refer to the respective markdown files or the main [README](../README.md).' >> "$README"
echo "Documentation TOC regenerated in $README" 