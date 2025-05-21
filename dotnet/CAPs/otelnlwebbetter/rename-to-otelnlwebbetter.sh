#!/bin/bash

set -e

OLD_NAME="otelbetter"
NEW_NAME="otelnlwebbetter"
NEW_NAMESPACE="$(echo "$NEW_NAME" | tr '[:lower:]' '[:upper:]' | cut -c1)$(echo "$NEW_NAME" | cut -c2-)"

echo "✏️ Adjusting project references from $OLD_NAME to $NEW_NAME"

# Step 1: Update content inside solution
echo "📝 Updating solution file..."
sed -i '' "s/$OLD_NAME/$NEW_NAME/g" "$NEW_NAME.sln"

# Step 2: Update csproj
echo "📝 Updating csproj file..."
sed -i '' "s/$OLD_NAME/$NEW_NAME/g" "$NEW_NAME.csproj"

# Step 3: Update namespaces and ActivitySource
echo "📝 Updating C# source files..."
find . -type f -name "*.cs" -exec sed -i '' "s/namespace $OLD_NAME/namespace $NEW_NAMESPACE/g" {} +
find . -type f -name "*.cs" -exec sed -i '' "s/ActivitySource(\"$OLD_NAME\"/ActivitySource(\"$NEW_NAME\"/g" {} +

echo "✅ Rename complete!"
