const
  gulp = requireModule("gulp-with-help"),
  installTools = requireModule("install-local-tools");

gulp.task("ensure-have-local-nuget", () => {
  return installTools.install([]);
});

