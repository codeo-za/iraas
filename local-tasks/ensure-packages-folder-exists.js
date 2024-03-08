const
  packagesFolder = require("./modules/config").packagesFolder,
  fs = requireModule("fs"),
  gulp = requireModule("gulp-with-help");

gulp.task("ensure-packages-folder-exists", ["clean-packages"], () => {
  return fs.ensureDirectoryExists(packagesFolder);
});

