#!/bin/bash
set -e  # Exit on error

echo "Starting deployment package script..."

# Copy deployment settings
echo "Copying deployment settings..."
cd src/IRAAS
cp appsettings.deploy.json appsettings.json
cd ../..
echo "Deployment settings copied."

# Set environment variables
export OCTOPUS_DEPLOY_FOLDER="./deploy_packages"
mkdir -p "$OCTOPUS_DEPLOY_FOLDER"
export DEBUG="*"

# Get version information
# Format: YYYY.M.D-RUNNUM-hash
DATE_VERSION=$(date +"%Y.%-m.%-d")  # YYYY.M.D format (no leading zeros for month/day)
RUN_NUMBER=$GITHUB_RUN_NUMBER  # Use GitHub run number
GIT_SHA=$(git rev-parse HEAD | cut -c1-7)  # First 7 chars of git SHA

NAME="$DATE_VERSION-$RUN_NUMBER-$GIT_SHA"

echo "Package Version: $NAME"

NUGET_PACKAGE_NAME="IRAAS.$NAME"

# Setting GitHub Actions outputs
echo "RELEASE_NAME=$NAME" >> $GITHUB_OUTPUT
echo "NUGET_PACKAGE_NAME=$NUGET_PACKAGE_NAME" >> $GITHUB_OUTPUT

# First publish to get all dependencies
echo "Publishing application..."
dotnet publish src/IRAAS/IRAAS.csproj \
  --configuration Release \
  --output "./publish" \
  --self-contained true

# copy deploy.sh to publish folder before nuget pack
cp src/IRAAS/deploy.sh ./publish/deploy.sh

# update version in nuspec file
cp src/IRAAS/Package.nuspec ./publish/
sed -i "s/<version>.*<\/version>/<version>$NAME<\/version>/" ./publish/Package.nuspec

# Then pack using the published output
echo "Creating NuGet package..."
dotnet pack src/IRAAS/IRAAS.csproj \
  --configuration Release \
  --output "$OCTOPUS_DEPLOY_FOLDER" \
  --include-symbols \
  -p:NuspecFile=../../publish/Package.nuspec \
  -p:NuspecBasePath=../../publish \
  -p:Version="$NAME" \
  -p:PackageId="IRAAS" \
  -p:AssemblyName="IRAAS"

# Push package to GitHub Packages NuGet repository
echo "Pushing package to GitHub Packages..."
PACKAGE_PATH="${OCTOPUS_DEPLOY_FOLDER}/${NUGET_PACKAGE_NAME}.nupkg"
echo "Expected package path: $PACKAGE_PATH"

if [ -f "$PACKAGE_PATH" ]; then
  echo "Pushing package to GitHub Packages..."
  dotnet nuget push "$PACKAGE_PATH" --api-key "$GHAUTH_TOKEN" --source "https://nuget.pkg.github.com/codeo-za/index.json" --skip-duplicate
  PUSH_RESULT=$?

  if [ $PUSH_RESULT -eq 0 ]; then
    echo "✅ Package push succeeded"
  else
    echo "❌ Package push failed with exit code: $PUSH_RESULT"
    exit $PUSH_RESULT
  fi
else
  echo "WARNING: Package not found at expected path."
  FOUND_PACKAGE=$(find . -name "*.nupkg" -type f | head -n 1)
  
  if [ -n "$FOUND_PACKAGE" ]; then
    echo "Found alternative package at: $FOUND_PACKAGE"
    # Extract actual package name for verification
    ACTUAL_PACKAGE_NAME=$(basename "$FOUND_PACKAGE" .nupkg)
    echo "Actual package name: $ACTUAL_PACKAGE_NAME"
    echo "Aborting..."
    exit 1
  fi
fi

# Octopus deployment function
deploy_octopus() {
    local project="$1"
    local change_set="$2"
    local deploy_to="$3"
    
    if [ -z "$project" ]; then
        echo "DeployOctopus Error: project is required."
        exit 1
    fi
    
    if [ -z "$change_set" ]; then
        echo "DeployOctopus Error: changeset is required."
        exit 1
    fi
    
    # NB: Octopus CLI v2+ now uses env vars OCTOPUS_URL and OCTOPUS_API_KEY for auth    
    # Create release
    echo "Creating Octopus release..."
    OCTO_CREATE="octopus release create --project '$project' --release-notes 'GitHub Actions Automated Release ($change_set)' --version '$change_set' --package-version '$change_set'"
    
    echo "Invoking: $OCTO_CREATE"
    eval "$OCTO_CREATE"
    
    if [ $? -ne 0 ]; then
        echo "Error executing Octopus - Creating Release"
        exit 1
    fi
    
    # Deploy release (only if environment specified)
    if [ -n "$deploy_to" ]; then
        echo "Deploying Octopus release..."
        OCTO_DEPLOY="octopus release deploy --project '$project' --version '$change_set' --environment '$deploy_to'"
        
        echo "Invoking: $OCTO_DEPLOY"
        eval "$OCTO_DEPLOY"
        
        if [ $? -ne 0 ]; then
            echo "Error executing Octopus - Deploying Release"
            exit 1
        fi
    fi
}

# Deploy to Octopus if not skipped
if [ "$SKIP_RELEASE" = "No" ]; then
    echo "Deployment started..."
    
    # Check if we have the required environment variables
    if [ -z "$OCTOPUS_SERVER_URL" ] || [ -z "$OCTOPUS_SERVER_API_KEY" ]; then
        echo "Error: OCTOPUS_SERVER_URL and OCTOPUS_SERVER_API_KEY must be set"
        exit 1
    fi
    
    # Using the deploy_octopus function with environment variables
    deploy_octopus "SPAR.IRAAS" "$NAME" "Azure - Silo Staging"
else
    echo "Skipping Octopus deployment as requested."
fi

echo "Deployment package script completed."
