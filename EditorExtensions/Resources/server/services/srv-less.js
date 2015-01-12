//#region Imports
var less = require("less"),
    fs = require("fs"),
    path = require("path");
//#endregion

//#region Handler
var handleLess = function (writer, params) {
	console.logLine('');
	console.logLine('(begin handleLess)');
	try {
		fs.readFile(params.sourceFileName, { encoding: 'utf8' }, function (err, data) {
			console.logLine('(handleLess marker 1 @ fs.readFile)');
			if (err) {
				console.logLine('(error handleLess @ fs.readFile)');
				console.logLine(err);
				writer.write(JSON.stringify({
					Success: false,
					SourceFileName: params.sourceFileName,
					TargetFileName: params.targetFileName,
					MapFileName: params.mapFileName,
					Remarks: "LESS: Error reading input file.",
					Details: err,
					Errors: [{
						Message: "LESS" + err,
						FileName: params.sourceFileName
					}]
				}));
				writer.end();
				return;
			}

			console.logLine('(handleLess marker 2 @ fs.readFile)');

			//data = data.replace('\uFEFF', '');  //strip BOM
			var css, map;
			var mapFileName = params.targetFileName + ".map";
			var sourceDir = path.dirname(params.sourceFileName);
			var options = {
				filename: params.sourceFileName,
				relativeUrls: true,
				paths: [sourceDir],
				sourceMap: {
					sourceMapFullFilename: mapFileName,
					sourceMapURL: params.sourceMapURL !== undefined ? path.basename(mapFileName) : null,
					sourceMapBasepath: sourceDir,
					sourceMapOutputFilename: path.basename(params.targetFileName),
					sourceMapRootpath: path.relative(path.dirname(params.targetFileName), sourceDir)
				},
				strictMath: params.strictMath !== undefined,
			};

			console.logLine('(handleLess marker 3 @ fs.readFile)');

			less.render(data, options)
				.then(function (output) {
					console.logLine('(handleLess marker 4 @ fs.readFile)');
					css = output.css;

					if (output.map)
						map = JSON.parse(output.map);

					console.logLine('(handleLess marker 5 @ fs.readFile)');

					if (params.autoprefixer !== undefined) {
						console.logLine('(handleLess marker 6 @ fs.readFile)');
						var autoprefixedOutput = require("./srv-autoprefixer")
												.processAutoprefixer(css, map, params.autoprefixerBrowsers,
																	 params.targetFileName, params.targetFileName);
						console.logLine('(handleLess marker 7 @ fs.readFile)');

						if (!autoprefixedOutput.Success) {
							console.logLine('(handleLess !autoprefixedOutput.Success)');
							writer.write(JSON.stringify({
								Success: false,
								SourceFileName: params.sourceFileName,
								TargetFileName: params.targetFileName,
								MapFileName: params.mapFileName,
								Remarks: "LESS: " + autoprefixedOutput.Remarks,
								Details: autoprefixedOutput.Remarks,
								Errors: [{
									Message: "LESS: " + autoprefixedOutput.Remarks,
									FileName: params.sourceFileName
								}]
							}));
							writer.end();
							return;
						}

						console.logLine('(handleLess marker 8 @ fs.readFile)');

						css = autoprefixedOutput.css;
						map = autoprefixedOutput.map;

						console.logLine('(handleLess marker 9 @ fs.readFile)');
					}

					console.logLine('(handleLess marker 10 @ fs.readFile)');

					if (params.rtlcss !== undefined) {
						console.logLine('(handleLess marker 11 @ fs.readFile)');

						var rtlTargetWithoutExtension = params.targetFileName.substr(0, params.targetFileName.lastIndexOf("."));
						var rtlTargetFileName = rtlTargetWithoutExtension + ".rtl.css";
						var rtlMapFileName = rtlTargetFileName + ".map";
						var rtlResult = require("./srv-rtlcss").processRtlCSS(css, map, params.targetFileName, rtlTargetFileName);

						if (rtlResult.Success) {
							console.logLine('(handleLess marker 12 @ fs.readFile)');
							writer.write(JSON.stringify({
								Success: true,
								SourceFileName: params.sourceFileName,
								TargetFileName: params.targetFileName,
								MapFileName: params.mapFileName,
								RtlSourceFileName: params.targetFileName,
								RtlTargetFileName: rtlTargetFileName,
								RtlMapFileName: rtlMapFileName,
								Remarks: "Successful!",
								Content: css,
								Map: JSON.stringify(map),
								RtlContent: rtlResult.css,
								RtlMap: JSON.stringify(rtlResult.map)
							}));

						} else {
							console.logLine('(handleLess marker 13 @ fs.readFile)');
							throw new Error("Error while processing RTLCSS");
						}
					} else {
						console.logLine('(handleLess marker 14 @ fs.readFile)');
						writer.write(JSON.stringify({
							Success: true,
							SourceFileName: params.sourceFileName,
							TargetFileName: params.targetFileName,
							MapFileName: params.mapFileName,
							Remarks: "Successful!",
							Content: css,
							Map: JSON.stringify(map)
						}));
					}

					console.logLine('(end handleLess @ done)');
					writer.end();
					return;
				},
				function (error) {
					console.logLine('(error handleLess @ less.render.then)');
					console.logLine(err);
					writer.write(JSON.stringify({
						Success: false,
						SourceFileName: params.sourceFileName,
						TargetFileName: params.targetFileName,
						MapFileName: params.mapFileName,
						Remarks: "LESS: Error parsing input file.",
						Details: error.message,
						Errors: [{
							Line: error.line,
							Column: error.column,
							Message: "LESS: " + error.message,
							FileName: error.filename
						}]
					}));
					writer.end();
					return;
				});
			console.logLine('(handleLess marker 15 @ fs.readFile)');
		});
	}
	catch (e) {
		console.logLine('');
		console.logLine('*** handleLess EXCEPTION ***');
		console.logLine(e.message);
		console.logLine('');
	}
	console.logLine('(end handleLess @ end of function)');
};
//#endregion

//#region Exports
module.exports = handleLess;
//#endregion
