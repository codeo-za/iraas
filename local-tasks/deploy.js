const target = process.env.OCTOPUS_DEPLOY_FOLDER,
  gulp = requireModule("gulp-with-help"),
  fs = requireModule("fs"),
  failMsg =
    "Please set OCTOPUS_DEPLOY_FOLDER environment variable to the target folder to deploy to";

gulp.task(
  "deploy",
  "Deploys nupkg to the feed",
  ["pack", "ensure-deploy-folder-exists"],
  () => {
    if (!target) {
      return Promise.reject(failMsg);
    }
    return gulp.src("packages/*.nupkg").pipe(gulp.dest(target));
  }
);

gulp.task("ensure-deploy-folder-exists", () => {
  if (!target) {
    return Promise.reject(failMsg);
  }
  return fs.ensureDirectoryExists(target);
});
