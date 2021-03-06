using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.NodeServices;
using Newtonsoft.Json;

namespace Mjml.AspNetCore
{
    public class MjmlServices : IMjmlServices, IDisposable
    {
        private readonly INodeServices _nodeServices;
        private readonly MjmlServiceOptions _options;

        private readonly StringAsTempFile _renderer;

        public MjmlServices(INodeServices nodeServices, MjmlServiceOptions options)
        {
            _nodeServices = nodeServices;
            _options = options;

            // setup renderer script
            var assembly = typeof(MjmlServices).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Mjml.AspNetCore.dist.renderer.js"))
            using (var reader = new StreamReader(stream))
            {
                var result = reader.ReadToEnd();
                _renderer = new StringAsTempFile(result, CancellationToken.None);
            }

            // force load the render script
            Warmup().Wait();
        }

        public void Dispose()
        {
            _nodeServices?.Dispose();
        }

        public Task<MjmlResponse> Render(string view)
        {
            return Render(view, CancellationToken.None);
        }

        /// <summary>
        /// Deserializes the json string to an object before shipment to NodeServices
        /// </summary>
        /// <param name="json">Valid JSON object describing mjml tree view</param>
        /// <returns></returns>
        public Task<MjmlResponse> RenderFromJson(string json)
        {
            var view = JsonConvert.DeserializeObject(json);
            return Render(view, CancellationToken.None);
        }

        public async Task<MjmlResponse> Render(object view, CancellationToken token)
        {
            var options = new MjmlRenderOptions()
            {
                KeepComments = _options.DefaultKeepComments,
                Beautify = _options.DefaultBeautify,
                Minify = false, // unsupported until we can re-add uglify
            };

            var args = new object[] { view, options };
            var result = await _nodeServices.InvokeAsync<MjmlResponse>(token, _renderer.FileName, args);

            return result;
        }

        private Task Warmup()
        {
            var emptyView = "<mjml></mjml>";
            return Render(emptyView, CancellationToken.None);
        }
    }
}
