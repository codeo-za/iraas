const
  promisifyFunction = requireModule("promisify-function"),
  Git = require("simple-git"),
  git = new Git(),
  revparse = promisifyFunction(git.revparse, git),
  getToolsFolder = requireModule("get-tools-folder"),
  throwIfNoFiles = requireModule("throw-if-no-files"),
  gulp = requireModule("gulp-with-help"),
  packagesFolder = require("./modules/config").packagesFolder,
  { pack } = requireModule("gulp-nuget-pack");

  // FIXME: gulp4 has (imo) a regression: whilst tasks can be overridden,
  // dependencies which are overridden after a dependant task aren't updated
  // for the dependant task
gulp.task(
  "pack",
  "Creates nupkgs from all nuspec files in this repo",
  ["dotnet-publish", "ensure-packages-folder-exists"], () => {
    const version = generatePackageVersion();
    return gulp.src([
      "**/*.nuspec",
      `!${getToolsFolder()}/**/*`
    ])
    .pipe(throwIfNoFiles("No nuspec files found"))
    .pipe(pack({
      version,
      basePath: "~/bin/Release/net6.0/publish"
    }))
    .pipe(gulp.dest(packagesFolder));
});

async function generatePackageVersion() {
  // we _cannot_ zero-pad date parts: nuget normalizes versions:
  // https://docs.microsoft.com/en-us/nuget/reference/package-versioning#normalized-version-numbers
  const
    now = new Date(),
    datePart = `${now.getFullYear()}.${now.getMonth() + 1}.${now.getDate()}`,
    buildNumber = (process.env.BUILD_NUMBER || "").replace(/[+.]/g, ""),
    sha = await revparse(["HEAD"]),
    revision = sha.substr(0, 12);
  return `${datePart}-${buildNumber}-${revision}`;
}
