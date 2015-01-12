//#region Imports
var autoprefixer = require("autoprefixer"),
    fs = require("fs");
//#endregion

//#region Process
var processAutoprefixer = function (cssContent, mapContent, browsers, sourceFileName, targetFileName) {
	console.logLine('');
	console.logLine('(begin processAutoprefixer)');
    var result = autoprefixer;

    if (browsers !== undefined)
        try {
			console.logLine('(processAutoprefixer marker 1)');
            result = autoprefixer(browsers.split(",").map(function (s) { return s.trim(); }));
        } catch (e) {
            // Return same css and map back so compilers can continue.
            console.logLine('(processAutoprefixer err: invalid browser provided)');
			return {
                Success: false,
                Remarks: "Invalid browser provided! See autoprefixer docs for list of valid browsers options.",
                css: cssContent,
                map: mapContent
            };
        }

	console.logLine('(processAutoprefixer marker 2)');
    if (!mapContent) {
		console.logLine('(end processAutoprefixer @ 1 -- success)');
        return {
            Success: true,
            css: result.process(cssContent).css
        };
	}

	console.logLine('(processAutoprefixer marker 3)');

    result = result.process(cssContent, {
        map: { prev: mapContent },
        from: sourceFileName,
        to: targetFileName
    });

	console.logLine('(handleAutoPrefixer marker 4)');

		// Curate maps
    mapContent = result.map.toJSON();

	console.logLine('(end processAutoprefixer @ 2 -- success)');

    return {
        Success: true,
        css: result.css,
        map: mapContent
    };
};
//#endregion

//#region Handler
var handleAutoPrefixer = function (writer, params) {
	console.logLine('(begin handleAutoPrefixer)');
    if (!fs.existsSync(params.sourceFileName)) {
		console.logLine('(handleAutoPrefixer marker 1)');
        writer.write(JSON.stringify({
            Success: false,
            SourceFileName: params.sourceFileName,
            TargetFileName: params.targetFileName,
            Remarks: "Autoprefixer: Input file not found!",
            Errors: [{
                Message: "Autoprefixer: Input file not found!",
                FileName: params.sourceFileName
            }]
        }));
        writer.end();
        return;
    }

	console.logLine('(handleAutoPrefixer marker 2)');
    fs.readFile(params.sourceFileName, 'utf8', function (err, data) {
        if (err) {
			console.logLine('(handleAutoPrefixer marker 3)');
            writer.write(JSON.stringify({
                Success: false,
                SourceFileName: params.sourceFileName,
                TargetFileName: params.targetFileName,
                Remarks: "Autoprefixer: Error reading input file.",
                Details: err,
                Errors: [{
                    Message: "Autoprefixer: " + err,
                    FileName: params.sourceFileName
                }]
            }));
            writer.end();
            return;
        }

		console.logLine('(handleAutoPrefixer marker 4)');

        var output = processAutoprefixer(data, null, params.autoprefixerBrowsers);

		console.logLine('(handleAutoPrefixer marker 5)');

        if (!output.Success) {
			console.logLine('(handleAutoPrefixer marker 6)');
            writer.write(JSON.stringify({
                Success: false,
                SourceFileName: params.sourceFileName,
                TargetFileName: params.targetFileName,
                Remarks: "Autoprefixer: " + output.Remarks,
                Errors: [{
                    Message: output.Remarks,
                    FileName: params.sourceFileName
                }]
            }));
        } else {
			console.logLine('(handleAutoPrefixer marker 7)');
            writer.write(JSON.stringify({
                Success: true,
                SourceFileName: params.sourceFileName,
                TargetFileName: params.targetFileName,
                Remarks: "Successful!",
                Content: output.css
            }));
        }

		console.logLine('(handleAutoPrefixer marker 8)');
        writer.end();
    });
	console.logLine('(handleAutoPrefixer marker 9)');
};
//#endregion

//#region Exports
module.exports = handleAutoPrefixer;
module.exports.processAutoprefixer = processAutoprefixer;
//#endregion
