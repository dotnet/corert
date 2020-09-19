
var DotNetSupportLib = {
    $DOTNET: {
        _dotnet_get_global: function () {
            function testGlobal(obj) {
                obj['___dotnet_global___'] = obj;
                var success = typeof ___dotnet_global___ === 'object' && obj['___dotnet_global___'] === obj;
                if (!success) {
                    delete obj['___dotnet_global___'];
                }
                return success;
            }
            if (typeof ___dotnet_global___ === 'object') {
                return ___dotnet_global___;
            }
            if (typeof global === 'object' && testGlobal(global)) {
                ___dotnet_global___ = global;
            } else if (typeof window === 'object' && testGlobal(window)) {
                ___dotnet_global___ = window;
            }
            if (typeof ___dotnet_global___ === 'object') {
                return ___dotnet_global___;
            }
            throw Error('unable to get DotNet global object.');
        },

    },
    
    corert_wasm_invoke_js: function (js, length, exception) {
        var jsFuncName = UTF8ToString(js, length);
        var res = eval(jsFuncName);
        exception = 0;
        return "" + res;
    },

    corert_wasm_invoke_js_unmarshalled: function (js, length, arg0, arg1, arg2, exception) {

        var jsFuncName = UTF8ToString(js, length);
        var dotNetExports = DOTNET._dotnet_get_global().DotNet;
        if (!dotNetExports) {
            throw new Error('The Microsoft.JSInterop.js library is not loaded.');
        }
        var funcInstance = dotNetExports.jsCallDispatcher.findJSFunction(jsFuncName);

        return funcInstance.call(null, arg0, arg1, arg2);
    },

};

autoAddDeps(DotNetSupportLib, '$DOTNET');
mergeInto(LibraryManager.library, DotNetSupportLib);



