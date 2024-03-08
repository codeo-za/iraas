const
  gulp = requireModule("gulp-with-help"),
  packagesFolder = require("./modules/config").packagesFolder,
  rimrafModule = require("rimraf"),
  rimraf = rimrafModule.sync;

gulp.task("clean-packages", async () => {
  rimraf(packagesFolder);
});
