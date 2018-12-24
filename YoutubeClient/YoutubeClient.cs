using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using AngleSharp.Dom.Html;
using AngleSharp.Dom;
using AngleSharp.Extensions;
using System.Diagnostics;

namespace YoutubeApi
{
    /// <summary>
    /// Media stream container type.
    /// </summary>
    public enum Container
    {
        /// <summary>
        /// MPEG-4 Part 14 (.mp4).
        /// </summary>
        Mp4,

        /// <summary>
        /// MPEG-4 Part 14 audio-only (.m4a).
        /// </summary>
        [Obsolete("Use Mp4 instead.")]
        M4A = Mp4,

        /// <summary>
        /// Web Media (.webm).
        /// </summary>
        WebM,

        /// <summary>
        /// 3rd Generation Partnership Project (.3gpp).
        /// </summary>
        Tgpp,

        /// <summary>
        /// Flash Video (.flv).
        /// </summary>
        [Obsolete("Not available anymore.")]
        Flv
    }

    public enum AudioEncoding
    {
        /// <summary>
        /// MPEG-2 Audio Layer III.
        /// </summary>
        [Obsolete("Not available anymore.")]
        Mp3,

        /// <summary>
        /// MPEG-4 Part 3, Advanced Audio Coding (AAC).
        /// </summary>
        Aac,

        /// <summary>
        /// Vorbis.
        /// </summary>
        Vorbis,

        /// <summary>
        /// Opus.
        /// </summary>
        Opus
    }

    public enum VideoEncoding
    {
        /// <summary>
        /// MPEG-4 Part 2.
        /// </summary>
        Mp4V,

        /// <summary>
        /// H263.
        /// </summary>
        [Obsolete("Not available anymore.")]
        H263,

        /// <summary>
        /// MPEG-4 Part 10, H264, Advanced Video Coding (AVC).
        /// </summary>
        H264,

        /// <summary>
        /// VP8.
        /// </summary>
        Vp8,

        /// <summary>
        /// VP9.
        /// </summary>
        Vp9,

        /// <summary>
        /// AV1.
        /// </summary>
        Av1
    }


    public enum VideoQuality
    {
        /// <summary>
        /// Low quality (144p).
        /// </summary>
        Low144,

        /// <summary>
        /// Low quality (240p).
        /// </summary>
        Low240,

        /// <summary>
        /// Medium quality (360p).
        /// </summary>
        Medium360,

        /// <summary>
        /// Medium quality (480p).
        /// </summary>
        Medium480,

        /// <summary>
        /// High quality (720p).
        /// </summary>
        High720,

        /// <summary>
        /// High quality (1080p).
        /// </summary>
        High1080,

        /// <summary>
        /// High quality (1440p).
        /// </summary>
        High1440,

        /// <summary>
        /// High quality (2160p).
        /// </summary>
        High2160,

        /// <summary>
        /// High quality (2880p).
        /// </summary>
        High2880,

        /// <summary>
        /// High quality (3072p).
        /// </summary>
        High3072,

        /// <summary>
        /// High quality (4320p).
        /// </summary>
        High4320
    }

    public struct VideoResolution : IEquatable<VideoResolution>
    {
        /// <summary>
        /// Viewport width.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Viewport height.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Initializes an instance of <see cref="VideoResolution"/>.
        /// </summary>
        public VideoResolution(int width, int height)
        {
            Width = width;//.GuardNotNegative(nameof(width));
            Height = height;//.GuardNotNegative(nameof(height));
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (obj is VideoResolution other)
                return Equals(other);

            return false;
        }

        /// <inheritdoc />
        public bool Equals(VideoResolution other)
        {
            return Width == other.Width && Height == other.Height;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        /// <inheritdoc />
        public override string ToString() => $"{Width}x{Height}";

        /// <summary />
        public static bool operator ==(VideoResolution r1, VideoResolution r2) => r1.Equals(r2);

        /// <summary />
        public static bool operator !=(VideoResolution r1, VideoResolution r2) => !(r1 == r2);
    }


    public class MediaStream : Stream
    {
        private readonly Stream _stream;

        /// <summary>
        /// Metadata associated with this stream.
        /// </summary>
        //[NotNull]
        public MediaStreamInfo Info { get; }

        /// <inheritdoc />
        public override bool CanRead => _stream.CanRead;

        /// <inheritdoc />
        public override bool CanSeek => _stream.CanSeek;

        /// <inheritdoc />
        public override bool CanWrite => _stream.CanWrite;

        /// <inheritdoc />
        public override long Length => Info.Size;

        /// <inheritdoc />
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        /// <summary>
        /// Initializes an instance of <see cref="MediaStream"/>.
        /// </summary>
        public MediaStream(MediaStreamInfo info, Stream stream)
        {
            Info = info;//info.GuardNotNull(nameof(info));
            _stream = stream;//stream.GuardNotNull(nameof(stream));
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken) => _stream.ReadAsync(buffer, offset, count, cancellationToken);

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);

        /// <inheritdoc />
        public override void Flush() => _stream.Flush();

        /// <inheritdoc />
        public override void SetLength(long value) => _stream.SetLength(value);

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

        /// <summary>
        /// Disposes resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _stream.Dispose();
        }
    }

    public abstract class MediaStreamInfo
    {
        /// <summary>
        /// Unique tag that identifies the properties of the associated stream.
        /// </summary>
        public int Itag { get; }

        /// <summary>
        /// URL of the endpoint that serves the associated stream.
        /// </summary>
        //[NotNull]
        public string Url { get; }

        /// <summary>
        /// Container type of the associated stream.
        /// </summary>
        public Container Container { get; }

        /// <summary>
        /// Content length (bytes) of the associated stream.
        /// </summary>
        public long Size { get; }

        /// <summary>
        /// Initializes an instance of <see cref="MediaStreamInfo"/>.
        /// </summary>
        protected MediaStreamInfo(int itag, string url, Container container, long size)
        {
            Itag = itag;
            Url = url;//url.GuardNotNull(nameof(url));
            Container = container;
            Size = size;//size.GuardNotNegative(nameof(size));
        }

        /// <inheritdoc />
        public override string ToString() => $"{Itag} ({Container})";
    }

    public class MuxedStreamInfo : MediaStreamInfo
    {
        /// <summary>
        /// Audio encoding of the associated stream.
        /// </summary>
        public AudioEncoding AudioEncoding { get; }

        /// <summary>
        /// Video encoding of the associated stream.
        /// </summary>
        public VideoEncoding VideoEncoding { get; }

        /// <summary>
        /// Video quality label of the associated stream.
        /// </summary>
        //[NotNull]
        public string VideoQualityLabel { get; }

        /// <summary>
        /// Video quality of the associated stream.
        /// </summary>
        public VideoQuality VideoQuality { get; }

        /// <summary>
        /// Video resolution of the associated stream.
        /// </summary>
        public VideoResolution Resolution { get; }

        /// <summary>
        /// Initializes an instance of <see cref="MuxedStreamInfo"/>.
        /// </summary>
        public MuxedStreamInfo(int itag, string url, Container container, long size, AudioEncoding audioEncoding,
            VideoEncoding videoEncoding, string videoQualityLabel, VideoQuality videoQuality,
            VideoResolution resolution)
            : base(itag, url, container, size)
        {
            AudioEncoding = audioEncoding;
            VideoEncoding = videoEncoding;
            VideoQualityLabel = videoQualityLabel;//.GuardNotNull(nameof(videoQualityLabel));
            VideoQuality = videoQuality;
            Resolution = resolution;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Itag} ({Container}) [muxed]";
    }

    public class AudioStreamInfo : MediaStreamInfo
    {
        /// <summary>
        /// Bitrate (bits/s) of the associated stream.
        /// </summary>
        public long Bitrate { get; }

        /// <summary>
        /// Audio encoding of the associated stream.
        /// </summary>
        public AudioEncoding AudioEncoding { get; }

        /// <summary>
        /// Initializes an instance of <see cref="AudioStreamInfo"/>.
        /// </summary>
        public AudioStreamInfo(int itag, string url, Container container, long size, long bitrate,
            AudioEncoding audioEncoding)
            : base(itag, url, container, size)
        {
            Bitrate = bitrate.GuardNotNegative(nameof(bitrate));
            AudioEncoding = audioEncoding;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Itag} ({Container}) [audio]";
    }

    public class VideoStreamInfo : MediaStreamInfo
    {
        /// <summary>
        /// Bitrate (bits/s) of the associated stream.
        /// </summary>
        public long Bitrate { get; }

        /// <summary>
        /// Video encoding of the associated stream.
        /// </summary>
        public VideoEncoding VideoEncoding { get; }

        /// <summary>
        /// Video quality label of the associated stream.
        /// </summary>
       // [NotNull]
        public string VideoQualityLabel { get; }

        /// <summary>
        /// Video quality of the associated stream.
        /// </summary>
        public VideoQuality VideoQuality { get; }

        /// <summary>
        /// Video resolution of the associated stream.
        /// </summary>
        public VideoResolution Resolution { get; }

        /// <summary>
        /// Video framerate (FPS) of the associated stream.
        /// </summary>
        public int Framerate { get; }

        /// <summary>
        /// Initializes an instance of <see cref="VideoStreamInfo"/>.
        /// </summary>
        public VideoStreamInfo(int itag, string url, Container container, long size, long bitrate,
            VideoEncoding videoEncoding, string videoQualityLabel, VideoQuality videoQuality,
            VideoResolution resolution, int framerate)
            : base(itag, url, container, size)
        {
            Bitrate = bitrate.GuardNotNegative(nameof(bitrate));
            VideoEncoding = videoEncoding;
            VideoQualityLabel = videoQualityLabel;//.GuardNotNull(nameof(videoQualityLabel));
            VideoQuality = videoQuality;
            Resolution = resolution;
            Framerate = framerate.GuardNotNegative(nameof(framerate));
        }

        /// <inheritdoc />
        public override string ToString() => $"{Itag} ({Container}) [video]";
    }

    /// <summary>
    /// Set of all available media stream infos.
    /// </summary>
    public class MediaStreamInfoSet
    {
        /// <summary>
        /// Muxed streams.
        /// </summary>
        //[NotNull, ItemNotNull]
        public IReadOnlyList<MuxedStreamInfo> Muxed { get; }

        /// <summary>
        /// Audio-only streams.
        /// </summary>
        //[NotNull, ItemNotNull]
        public IReadOnlyList<AudioStreamInfo> Audio { get; }

        /// <summary>
        /// Video-only streams.
        /// </summary>
       // [NotNull, ItemNotNull]
        public IReadOnlyList<VideoStreamInfo> Video { get; }

        /// <summary>
        /// Raw HTTP Live Streaming (HLS) URL to the m3u8 playlist.
        /// Null if not a live stream.
        /// </summary>
        //[CanBeNull]
        public string HlsLiveStreamUrl { get; }

        /// <summary>
        /// Expiry date for this information.
        /// </summary>
        public DateTimeOffset ValidUntil { get; }

        /// <summary>
        /// Initializes an instance of <see cref="MediaStreamInfoSet"/>.
        /// </summary>
        public MediaStreamInfoSet(IReadOnlyList<MuxedStreamInfo> muxed,
            IReadOnlyList<AudioStreamInfo> audio,
            IReadOnlyList<VideoStreamInfo> video,
            string hlsLiveStreamUrl,
            DateTimeOffset validUntil)
        {
            Muxed = muxed;//muxed.GuardNotNull(nameof(muxed));
            Audio = audio;//.GuardNotNull(nameof(audio));
            Video = video;//.GuardNotNull(nameof(video));
            HlsLiveStreamUrl = hlsLiveStreamUrl;
            ValidUntil = validUntil;
        }
    }

    public class YoutubeClient : IDisposable
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly HttpClient httpClient;
        public YoutubeClient()
        {
            logger.Debug("YoutubeClient()");

            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }
            
            handler.UseCookies = false;
            logger.Debug("new HttpClient()");
            httpClient = new HttpClient(handler, true);
        }

        /// <summary>
        /// Verifies that the given string is syntactically a valid YouTube video ID.
        /// </summary>
        public static bool ValidateVideoId(string videoId)
        {
            if (videoId.IsBlank())
                return false;

            // Video IDs are always 11 characters
            if (videoId.Length != 11)
                return false;

            return !Regex.IsMatch(videoId, @"[^0-9a-zA-Z_\-]");
        }

        /// <summary>
        /// Tries to parse video ID from a YouTube video URL.
        /// </summary>
        public static bool TryParseVideoId(string videoUrl, out string videoId)
        {
            logger.Debug("TryParseVideoId(...)");

            videoId = default(string);

            if (videoUrl.IsBlank())
                return false;

            // https://www.youtube.com/watch?v=yIVRs6YSbOM
            var regularMatch = Regex.Match(videoUrl, @"youtube\..+?/watch.*?v=(.*?)(?:&|/|$)").Groups[1].Value;
            if (regularMatch.IsNotBlank() && ValidateVideoId(regularMatch))
            {
                videoId = regularMatch;
                return true;
            }

            // https://youtu.be/yIVRs6YSbOM
            var shortMatch = Regex.Match(videoUrl, @"youtu\.be/(.*?)(?:\?|&|/|$)").Groups[1].Value;
            if (shortMatch.IsNotBlank() && ValidateVideoId(shortMatch))
            {
                videoId = shortMatch;
                return true;
            }

            // https://www.youtube.com/embed/yIVRs6YSbOM
            var embedMatch = Regex.Match(videoUrl, @"youtube\..+?/embed/(.*?)(?:\?|&|/|$)").Groups[1].Value;
            if (embedMatch.IsNotBlank() && ValidateVideoId(embedMatch))
            {
                videoId = embedMatch;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Parses video ID from a YouTube video URL.
        /// </summary>
        public static string ParseVideoId(string videoUrl)
        {
            //videoUrl.GuardNotNull(nameof(videoUrl));

            return TryParseVideoId(videoUrl, out var result)
                ? result
                : throw new FormatException($"Could not parse video ID from given string [{videoUrl}].");
        }


        /// <inheritdoc />
        public async Task<MediaStreamInfoSet> GetVideoMediaStreamInfosAsync(string videoId)
        {
            logger.Debug("GetVideoMediaStreamInfosAsync(...)");

            //videoId.GuardNotNull(nameof(videoId));

            if (!ValidateVideoId(videoId))
                throw new ArgumentException($"Invalid YouTube video ID [{videoId}].", nameof(videoId));

            // Register the time at which the request was made to calculate expiry date later on
            var requestedAt = DateTimeOffset.Now;

            // Get parser
            var parser = await GetPlayerResponseParserAsync(videoId, true).ConfigureAwait(false);

            // Prepare stream info maps
            var muxedStreamInfoMap = new Dictionary<int, MuxedStreamInfo>();
            var audioStreamInfoMap = new Dictionary<int, AudioStreamInfo>();
            var videoStreamInfoMap = new Dictionary<int, VideoStreamInfo>();


            // Parse muxed stream infos
            foreach (var streamInfoParser in parser.GetMuxedStreamInfos())
            {
                // Parse info
                var itag = streamInfoParser.ParseItag();
                var url = streamInfoParser.ParseUrl();

                // Try to parse content length, otherwise get it manually
                var contentLength = streamInfoParser.ParseContentLength();
                if (contentLength <= 0)
                {
                    // Send HEAD request and get content length
                    contentLength = await httpClient.GetContentLengthAsync(url, false).ConfigureAwait(false) ?? -1;

                    // If content length is still not available - stream is gone or faulty
                    if (contentLength <= 0) continue;
                }

                // Parse container
                var containerStr = streamInfoParser.ParseContainer();
                var container = MediaHelper.ContainerFromString(containerStr);

                // Parse audio encoding
                var audioEncodingStr = streamInfoParser.ParseAudioEncoding();
                var audioEncoding = MediaHelper.AudioEncodingFromString(audioEncodingStr);

                // Parse video encoding
                var videoEncodingStr = streamInfoParser.ParseVideoEncoding();
                var videoEncoding = MediaHelper.VideoEncodingFromString(videoEncodingStr);

                // Parse video quality label and video quality
                var videoQualityLabel = streamInfoParser.ParseVideoQualityLabel();
                var videoQuality = VideoQualityHelper.VideoQualityFromLabel(videoQualityLabel);

                // Parse resolution
                var width = streamInfoParser.ParseWidth();
                var height = streamInfoParser.ParseHeight();
                var resolution = new VideoResolution(width, height);

                // Add stream
                var streamInfo = new MuxedStreamInfo(itag, url, container, contentLength, audioEncoding, videoEncoding,
                    videoQualityLabel, videoQuality, resolution);
                muxedStreamInfoMap[itag] = streamInfo;
            }

            // Parse adaptive stream infos
            foreach (var streamInfoParser in parser.GetAdaptiveStreamInfos())
            {
                // Parse info
                var itag = streamInfoParser.ParseItag();
                var url = streamInfoParser.ParseUrl();
                var bitrate = streamInfoParser.ParseBitrate();

                // Try to parse content length, otherwise get it manually
                var contentLength = streamInfoParser.ParseContentLength();
                if (contentLength <= 0)
                {
                    // Send HEAD request and get content length
                    contentLength = await httpClient.GetContentLengthAsync(url, false).ConfigureAwait(false) ?? -1;

                    // If content length is still not available - stream is gone or faulty
                    if (contentLength <= 0) continue;
                }

                // Parse container
                var containerStr = streamInfoParser.ParseContainer();
                var container = MediaHelper.ContainerFromString(containerStr);

                // If audio-only
                if (streamInfoParser.ParseIsAudioOnly())
                {
                    // Parse audio encoding
                    var audioEncodingStr = streamInfoParser.ParseAudioEncoding();
                    var audioEncoding = MediaHelper.AudioEncodingFromString(audioEncodingStr);

                    // Add stream
                    var streamInfo = new AudioStreamInfo(itag, url, container, contentLength, bitrate, audioEncoding);
                    audioStreamInfoMap[itag] = streamInfo;
                }
                // If video-only
                else
                {
                    // Parse video encoding
                    var videoEncodingStr = streamInfoParser.ParseVideoEncoding();
                    var videoEncoding = MediaHelper.VideoEncodingFromString(videoEncodingStr);

                    // Parse video quality label and video quality
                    var videoQualityLabel = streamInfoParser.ParseVideoQualityLabel();
                    var videoQuality = VideoQualityHelper.VideoQualityFromLabel(videoQualityLabel);

                    // Parse resolution
                    var width = streamInfoParser.ParseWidth();
                    var height = streamInfoParser.ParseHeight();
                    var resolution = new VideoResolution(width, height);

                    // Parse framerate
                    var framerate = streamInfoParser.ParseFramerate();

                    // Add stream
                    var streamInfo = new VideoStreamInfo(itag, url, container, contentLength, bitrate, videoEncoding,
                        videoQualityLabel, videoQuality, resolution, framerate);
                    videoStreamInfoMap[itag] = streamInfo;
                }
            }

            // Parse dash manifest
            var dashManifestUrl = parser.ParseDashManifestUrl();
            if (dashManifestUrl.IsNotBlank())
            {
                // Get the dash manifest parser
                var dashManifestParser = await GetDashManifestParserAsync(dashManifestUrl).ConfigureAwait(false);

                // Parse dash stream infos
                foreach (var streamInfoParser in dashManifestParser.GetStreamInfos())
                {
                    // Parse info
                    var itag = streamInfoParser.ParseItag();
                    var url = streamInfoParser.ParseUrl();
                    var contentLength = streamInfoParser.ParseContentLength();
                    var bitrate = streamInfoParser.ParseBitrate();

                    // Parse container
                    var containerStr = streamInfoParser.ParseContainer();
                    var container = MediaHelper.ContainerFromString(containerStr);

                    // If audio-only
                    if (streamInfoParser.ParseIsAudioOnly())
                    {
                        // Parse audio encoding
                        var audioEncodingStr = streamInfoParser.ParseEncoding();
                        var audioEncoding = MediaHelper.AudioEncodingFromString(audioEncodingStr);

                        // Add stream
                        var streamInfo =
                            new AudioStreamInfo(itag, url, container, contentLength, bitrate, audioEncoding);
                        audioStreamInfoMap[itag] = streamInfo;
                    }
                    // If video-only
                    else
                    {
                        // Parse video encoding
                        var videoEncodingStr = streamInfoParser.ParseEncoding();
                        var videoEncoding = MediaHelper.VideoEncodingFromString(videoEncodingStr);

                        // Parse resolution
                        var width = streamInfoParser.ParseWidth();
                        var height = streamInfoParser.ParseHeight();
                        var resolution = new VideoResolution(width, height);

                        // Parse framerate
                        var framerate = streamInfoParser.ParseFramerate();

                        // Determine video quality from height
                        var videoQuality = VideoQualityHelper.VideoQualityFromHeight(height);

                        // Determine video quality label from video quality and framerate
                        var videoQualityLabel = VideoQualityHelper.VideoQualityToLabel(videoQuality, framerate);

                        // Add stream
                        var streamInfo = new VideoStreamInfo(itag, url, container, contentLength, bitrate,
                            videoEncoding, videoQualityLabel, videoQuality, resolution, framerate);
                        videoStreamInfoMap[itag] = streamInfo;
                    }
                }
            }

            // Finalize stream info collections
            var muxedStreamInfos = muxedStreamInfoMap.Values.OrderByDescending(s => s.VideoQuality).ToArray();
            var audioStreamInfos = audioStreamInfoMap.Values.OrderByDescending(s => s.Bitrate).ToArray();
            var videoStreamInfos = videoStreamInfoMap.Values.OrderByDescending(s => s.VideoQuality).ToArray();

            // Get the HLS manifest URL if available
            var hlsManifestUrl = parser.ParseHlsManifestUrl();

            // Get expiry date
            var expiresIn = parser.ParseStreamInfoSetExpiresIn();
            var validUntil = requestedAt.Add(expiresIn);

            return new MediaStreamInfoSet(muxedStreamInfos, audioStreamInfos, videoStreamInfos, hlsManifestUrl,
                validUntil);
        }


        private async Task<PlayerResponseParser> GetPlayerResponseParserAsync(string videoId, bool ensureIsPlayable = false)
        {
            logger.Debug("GetPlayerResponseParserAsync(...)");

            // Get player response parser via video info (this works for most videos)
            var videoInfoParser = await GetVideoInfoParserAsync(videoId).ConfigureAwait(false);
            var playerResponseParser = videoInfoParser.GetPlayerResponse();

            // If the video is not available - throw exception
            if (!playerResponseParser.ParseIsAvailable())
            {
                var errorReason = playerResponseParser.ParseErrorReason();
                throw new VideoUnavailableException(videoId,
                    $"Video [{videoId}] is unavailable. (Reason: {errorReason})");
            }

            // If asked to ensure playability, but the video is not playable - retry
            if (ensureIsPlayable && !playerResponseParser.ParseIsPlayable())
            {
                // Get player response parser via watch page (this works for some other videos)
                var watchPageParser = await GetVideoWatchPageParserAsync(videoId).ConfigureAwait(false);
                var watchPageConfigParser = watchPageParser.GetConfig();
                playerResponseParser = watchPageConfigParser.GetPlayerResponse();

                // If the video is still not playable - throw exception
                if (!playerResponseParser.ParseIsPlayable())
                {
                    var errorReason = playerResponseParser.ParseErrorReason();

                    // If the video is not playable because it requires purchase - throw specific exception
                    var previewVideoId = watchPageConfigParser.ParsePreviewVideoId();
                    if (previewVideoId.IsNotBlank())
                    {
                        throw new VideoRequiresPurchaseException(previewVideoId, videoId,
                            $"Video [{videoId}] is unplayable because it requires purchase. (Reason: {errorReason})");
                    }
                    // For other reasons - throw a generic exception
                    else
                    {
                        throw new VideoUnplayableException(videoId,
                            $"Video [{videoId}] is unplayable. (Reason: {errorReason})");
                    }
                }
            }

            return playerResponseParser;
        }

        private async Task<VideoWatchPageParser> GetVideoWatchPageParserAsync(string videoId)
        {
            logger.Debug("GetVideoWatchPageParserAsync(...)");

            var url = $"https://www.youtube.com/watch?v={videoId}&disable_polymer=true&bpctr=9999999999&hl=en";
            var raw = await httpClient.GetStringAsync(url).ConfigureAwait(false);

            return VideoWatchPageParser.Initialize(raw);
        }


        private async Task<VideoInfoParser> GetVideoInfoParserAsync(string videoId, string el = "embedded")
        {
            logger.Debug("GetVideoInfoParserAsync(...)");

            // This parameter does magic and a lot of videos don't work without it
            var eurl = $"https://youtube.googleapis.com/v/{videoId}".UrlEncode();

            var url = $"https://www.youtube.com/get_video_info?video_id={videoId}&el={el}&eurl={eurl}&hl=en";
            var raw = await httpClient.GetStringAsync(url).ConfigureAwait(false);

            return VideoInfoParser.Initialize(raw);
        }

        private async Task<DashManifestParser> GetDashManifestParserAsync(string dashManifestUrl)
        {
            logger.Debug("GetDashManifestParserAsync(...)");
            var raw = await httpClient.GetStringAsync(dashManifestUrl).ConfigureAwait(false);
            return DashManifestParser.Initialize(raw);
        }

        /// <inheritdoc />
        public Task<MediaStream> GetMediaStreamAsync(MediaStreamInfo info)
        {
            //info.GuardNotNull(nameof(info));

            // Determine if stream is rate-limited
            var isRateLimited = !Regex.IsMatch(info.Url, @"ratebypass[=/]yes");

            // Determine segment size
            var segmentSize = isRateLimited
                ? 9_898_989 // this number was carefully devised through research
                : long.MaxValue; // don't use segmentation for non-rate-limited streams

            // Get segmented stream
            var stream = httpClient.CreateSegmentedStream(info.Url, info.Size, segmentSize);

            // This method must return a task for backwards-compatibility reasons
            return Task.FromResult(new MediaStream(info, stream));
        }


        /// <inheritdoc />
        public async Task DownloadMediaStreamAsync(MediaStreamInfo info, string filePath,
            IProgress<double> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            //filePath.GuardNotNull(nameof(filePath));

            using (var output = File.Create(filePath))
                await DownloadMediaStreamAsync(info, output, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DownloadMediaStreamAsync(MediaStreamInfo info, Stream output,
            IProgress<double> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            //info.GuardNotNull(nameof(info));
            // output.GuardNotNull(nameof(output));

            using (var input = await GetMediaStreamAsync(info).ConfigureAwait(false))
                await input.CopyToAsync(output, progress, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            logger.Debug("YoutubeClient::Dispose()");
            httpClient?.Dispose();
        }

        #region Internals
        internal static class MediaHelper
        {
            public static Container ContainerFromString(string str)
            {
                if (str.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                    return Container.Mp4;

                if (str.Equals("webm", StringComparison.OrdinalIgnoreCase))
                    return Container.WebM;

                if (str.Equals("3gpp", StringComparison.OrdinalIgnoreCase))
                    return Container.Tgpp;

                // Unknown
                throw new ArgumentOutOfRangeException(nameof(str), $"Unknown container [{str}].");
            }

            public static string ContainerToFileExtension(Container container)
            {
                // Tgpp gets special treatment
                if (container == Container.Tgpp)
                    return "3gpp";

                // Convert to lower case string
                return container.ToString().ToLowerInvariant();
            }
            public static AudioEncoding AudioEncodingFromString(string str)
            {
                if (str.StartsWith("mp4a", StringComparison.OrdinalIgnoreCase))
                    return AudioEncoding.Aac;

                if (str.StartsWith("vorbis", StringComparison.OrdinalIgnoreCase))
                    return AudioEncoding.Vorbis;

                if (str.StartsWith("opus", StringComparison.OrdinalIgnoreCase))
                    return AudioEncoding.Opus;

                // Unknown
                throw new ArgumentOutOfRangeException(nameof(str), $"Unknown encoding [{str}].");
            }

            public static VideoEncoding VideoEncodingFromString(string str)
            {
                if (str.StartsWith("mp4v", StringComparison.OrdinalIgnoreCase))
                    return VideoEncoding.Mp4V;

                if (str.StartsWith("avc1", StringComparison.OrdinalIgnoreCase))
                    return VideoEncoding.H264;

                if (str.StartsWith("vp8", StringComparison.OrdinalIgnoreCase))
                    return VideoEncoding.Vp8;

                if (str.StartsWith("vp9", StringComparison.OrdinalIgnoreCase))
                    return VideoEncoding.Vp9;

                if (str.StartsWith("av01", StringComparison.OrdinalIgnoreCase))
                    return VideoEncoding.Av1;

                // Unknown
                throw new ArgumentOutOfRangeException(nameof(str), $"Unknown encoding [{str}].");
            }
        }

        internal static class VideoQualityHelper
        {
            private static readonly Dictionary<int, VideoQuality> HeightToVideoQualityMap =
                Enum.GetValues(typeof(VideoQuality)).Cast<VideoQuality>().ToDictionary(
                    v => v.ToString().StripNonDigit().ParseInt(), // High1080 => 1080
                    v => v);

            public static VideoQuality VideoQualityFromHeight(int height)
            {
                // Find the video quality by height (highest video quality that has height below or equal to given)
                var matchingHeight = HeightToVideoQualityMap.Keys.LastOrDefault(h => h <= height);

                // Return video quality
                return matchingHeight > 0
                    ? HeightToVideoQualityMap[matchingHeight] // if found - return matching quality
                    : HeightToVideoQualityMap.Values.First(); // otherwise return lowest available quality
            }

            public static VideoQuality VideoQualityFromLabel(string label)
            {
                // Strip "p" and framerate to get height (e.g. >1080<p60)
                var heightStr = label.SubstringUntil("p");
                var height = heightStr.ParseInt();

                return VideoQualityFromHeight(height);
            }

            public static string VideoQualityToLabel(VideoQuality quality)
            {
                // Convert to string, strip non-digits and add "p"
                return quality.ToString().StripNonDigit() + "p";
            }

            public static string VideoQualityToLabel(VideoQuality quality, int framerate)
            {
                // Framerate appears only if it's above 30
                if (framerate <= 30)
                    return VideoQualityToLabel(quality);

                // YouTube rounds framerate to nearest next ten
                var framerateRounded = (int)Math.Ceiling(framerate / 10.0) * 10;
                return VideoQualityToLabel(quality) + framerateRounded;
            }
        }

        internal class VideoInfoParser
        {
            private readonly IReadOnlyDictionary<string, string> _root;

            public VideoInfoParser(IReadOnlyDictionary<string, string> root)
            {
                _root = root;
            }

            public PlayerResponseParser GetPlayerResponse()
            {
                // Extract player response
                var playerResponseRaw = _root["player_response"];
                var playerResponseJson = JToken.Parse(playerResponseRaw);

                return new PlayerResponseParser(playerResponseJson);
            }

            public static VideoInfoParser Initialize(string raw)
            {
                var root = UrlEx.SplitQuery(raw);
                return new VideoInfoParser(root);
            }
        }

        internal class PlayerResponseParser
        {
            private readonly JToken _root;

            public PlayerResponseParser(JToken root)
            {
                _root = root;
            }

            public bool ParseIsAvailable() => _root.SelectToken("videoDetails") != null;

            public bool ParseIsPlayable()
            {
                var playabilityStatusValue = _root.SelectToken("playabilityStatus.status")?.Value<string>();
                return string.Equals(playabilityStatusValue, "OK", StringComparison.OrdinalIgnoreCase);
            }

            public string ParseErrorReason() => _root.SelectToken("playabilityStatus.reason")?.Value<string>();

            public string ParseAuthor() => _root.SelectToken("videoDetails.author").Value<string>();

            public string ParseChannelId() => _root.SelectToken("videoDetails.channelId").Value<string>();

            public string ParseTitle() => _root.SelectToken("videoDetails.title").Value<string>();

            public TimeSpan ParseDuration()
            {
                var durationSeconds = _root.SelectToken("videoDetails.lengthSeconds").Value<double>();
                return TimeSpan.FromSeconds(durationSeconds);
            }

            public IReadOnlyList<string> ParseKeywords() =>
                _root.SelectToken("videoDetails.keywords").EmptyIfNull().Values<string>().ToArray();

            public bool ParseIsLiveStream() => _root.SelectToken("videoDetails.isLiveContent")?.Value<bool>() == true;

            public string ParseDashManifestUrl()
            {
                // HACK: Don't return DASH manifest URL if it's a live stream
                // I'm not sure how to handle these streams yet
                if (ParseIsLiveStream())
                    return null;

                return _root.SelectToken("streamingData.dashManifestUrl")?.Value<string>();
            }

            public string ParseHlsManifestUrl() => _root.SelectToken("streamingData.hlsManifestUrl")?.Value<string>();

            public TimeSpan ParseStreamInfoSetExpiresIn()
            {
                var expiresInSeconds = _root.SelectToken("streamingData.expiresInSeconds").Value<double>();
                return TimeSpan.FromSeconds(expiresInSeconds);
            }

            public IEnumerable<StreamInfoParser> GetMuxedStreamInfos()
            {
                // HACK: Don't return streams if it's a live stream
                // I'm not sure how to handle these streams yet
                if (ParseIsLiveStream())
                    return Enumerable.Empty<StreamInfoParser>();

                return _root.SelectToken("streamingData.formats").EmptyIfNull().Select(j => new StreamInfoParser(j));
            }

            public IEnumerable<StreamInfoParser> GetAdaptiveStreamInfos()
            {
                // HACK: Don't return streams if it's a live stream
                // I'm not sure how to handle these streams yet
                if (ParseIsLiveStream())
                    return Enumerable.Empty<StreamInfoParser>();

                return _root.SelectToken("streamingData.adaptiveFormats").EmptyIfNull()
                    .Select(j => new StreamInfoParser(j));
            }

            public IEnumerable<ClosedCaptionTrackInfoParser> GetClosedCaptionTrackInfos()
                => _root.SelectToken("captions.playerCaptionsTracklistRenderer.captionTracks").EmptyIfNull()
                    .Select(t => new ClosedCaptionTrackInfoParser(t));

            public class StreamInfoParser
            {
                private readonly JToken _root;

                public StreamInfoParser(JToken root)
                {
                    _root = root;
                }

                public int ParseItag() => _root.SelectToken("itag").Value<int>();

                public string ParseUrl() => _root.SelectToken("url").Value<string>();

                public long ParseContentLength() => _root.SelectToken("contentLength")?.Value<long>() ?? -1;

                public long ParseBitrate() => _root.SelectToken("bitrate").Value<long>();

                public string ParseMimeType() => _root.SelectToken("mimeType").Value<string>();

                public string ParseContainer() => ParseMimeType().SubstringUntil(";").SubstringAfter("/");

                public string ParseAudioEncoding() => ParseMimeType().SubstringAfter("codecs=\"").SubstringUntil("\"")
                    .Split(", ").LastOrDefault(); // audio codec is either the only codec or the second (last) codec

                public string ParseVideoEncoding() => ParseMimeType().SubstringAfter("codecs=\"").SubstringUntil("\"")
                    .Split(", ").FirstOrDefault(); // video codec is either the only codec or the first codec

                public bool ParseIsAudioOnly() => ParseMimeType().StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

                public int ParseWidth() => _root.SelectToken("width").Value<int>();

                public int ParseHeight() => _root.SelectToken("height").Value<int>();

                public int ParseFramerate() => _root.SelectToken("fps").Value<int>();

                public string ParseVideoQualityLabel() => _root.SelectToken("qualityLabel").Value<string>();
            }

            public class ClosedCaptionTrackInfoParser
            {
                private readonly JToken _root;

                public ClosedCaptionTrackInfoParser(JToken root)
                {
                    _root = root;
                }

                public string ParseUrl() => _root.SelectToken("baseUrl").Value<string>();

                public string ParseLanguageCode() => _root.SelectToken("languageCode").Value<string>();

                public string ParseLanguageName() => _root.SelectToken("name.simpleText").Value<string>();

                public bool ParseIsAutoGenerated() => _root.SelectToken("vssId").Value<string>()
                    .StartsWith("a.", StringComparison.OrdinalIgnoreCase);
            }
        }

        internal class DashManifestParser
        {
            private readonly XElement _root;

            public DashManifestParser(XElement root)
            {
                _root = root;
            }

            public static DashManifestParser Initialize(string raw)
            {
                var root = XElement.Parse(raw).StripNamespaces();
                return new DashManifestParser(root);
            }

            public IEnumerable<StreamInfoParser> GetStreamInfos()
            {
                var streamInfosXml = _root.Descendants("Representation");

                // Filter out partial streams
                streamInfosXml = streamInfosXml.Where(s =>
                    s.Descendants("Initialization").FirstOrDefault()?.Attribute("sourceURL")?.Value.Contains("sq/") !=
                    true);

                return streamInfosXml.Select(x => new StreamInfoParser(x));
            }

            public class StreamInfoParser
            {
                private readonly XElement _root;

                public StreamInfoParser(XElement root)
                {
                    _root = root;
                }

                public int ParseItag() => (int)_root.Attribute("id");

                public string ParseUrl() => (string)_root.Element("BaseURL");

                public long ParseContentLength() => Regex.Match(ParseUrl(), @"clen[/=](\d+)").Groups[1].Value.ParseLong();

                public long ParseBitrate() => (long)_root.Attribute("bandwidth");

                public string ParseContainer() => Regex.Match(ParseUrl(), @"mime[/=]\w*%2F([\w\d]*)").Groups[1].Value.UrlDecode();

                public string ParseEncoding() => (string)_root.Attribute("codecs");

                public bool ParseIsAudioOnly() => _root.Element("AudioChannelConfiguration") != null;

                public int ParseWidth() => (int)_root.Attribute("width");

                public int ParseHeight() => (int)_root.Attribute("height");

                public int ParseFramerate() => (int)_root.Attribute("frameRate");
            }
        }

        internal class VideoWatchPageParser
        {
            private readonly IHtmlDocument _root;

            public VideoWatchPageParser(IHtmlDocument root)
            {
                _root = root;
            }

            public static VideoWatchPageParser Initialize(string raw)
            {

                var root = new AngleSharp.Parser.Html.HtmlParser().Parse(raw);
                return new VideoWatchPageParser(root);
            }

            public DateTimeOffset ParseUploadDate() => _root.QuerySelector("meta[itemprop=\"datePublished\"]")
                .GetAttribute("content").ParseDateTimeOffset("yyyy-MM-dd");

            public string ParseDescription()
            {
                var buffer = new StringBuilder();

                var descriptionNode = _root.QuerySelector("p#eow-description");
                var childNodes = descriptionNode.ChildNodes;

                foreach (var childNode in childNodes)
                {
                    if (childNode.NodeType == NodeType.Text)
                    {
                        buffer.Append(childNode.TextContent);
                    }
                    else if (childNode is IHtmlAnchorElement anchorNode)
                    {
                        // If it uses YouTube redirect - get the actual link
                        if (anchorNode.PathName.Equals("/redirect", StringComparison.OrdinalIgnoreCase))
                        {
                            // Get query parameters
                            var queryParams = UrlEx.SplitQuery(anchorNode.Search);

                            // Get the actual href
                            var actualHref = queryParams["q"].UrlDecode();

                            buffer.Append(actualHref);
                        }
                        else
                        {
                            buffer.Append(anchorNode.TextContent);
                        }
                    }
                    else if (childNode is IHtmlBreakRowElement)
                    {
                        buffer.AppendLine();
                    }
                }

                return buffer.ToString();
            }

            public long ParseViewCount() => _root.QuerySelector("meta[itemprop=\"interactionCount\"]")
                ?.GetAttribute("content").ParseLongOrDefault() ?? 0;

            public long ParseLikeCount() => _root.QuerySelector("button.like-button-renderer-like-button")?.Text()
                .StripNonDigit().ParseLongOrDefault() ?? 0;

            public long ParseDislikeCount() => _root.QuerySelector("button.like-button-renderer-dislike-button")?.Text()
                .StripNonDigit().ParseLongOrDefault() ?? 0;

            public ConfigParser GetConfig()
            {
                var configRaw = Regex.Match(_root.Source.Text,
                        @"ytplayer\.config = (?<Json>\{[^\{\}]*(((?<Open>\{)[^\{\}]*)+((?<Close-Open>\})[^\{\}]*)+)*(?(Open)(?!))\})")
                    .Groups["Json"].Value;
                var configJson = JToken.Parse(configRaw);

                return new ConfigParser(configJson);
            }

            public class ConfigParser
            {
                private readonly JToken _root;

                public ConfigParser(JToken root)
                {
                    _root = root;
                }

                public string ParsePreviewVideoId() => _root.SelectToken("args.ypc_vid")?.Value<string>();

                public PlayerResponseParser GetPlayerResponse()
                {
                    // Player response is a json, which is stored as a string, inside json
                    var playerResponseRaw = _root.SelectToken("args.player_response").Value<string>();
                    var playerResponseJson = JToken.Parse(playerResponseRaw);

                    return new PlayerResponseParser(playerResponseJson);
                }
            }

        }
        #endregion
    }


    internal static class Extensions
    {

        public static async Task<HttpResponseMessage> HeadAsync(this HttpClient client, string requestUri)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Head, requestUri))
                return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        }

        public static async Task<string> GetStringAsync(this HttpClient client, string requestUri,
            bool ensureSuccess = true)
        {
            using (var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead)
                .ConfigureAwait(false))
            {
                if (ensureSuccess)
                    response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        public static async Task<Stream> GetStreamAsync(this HttpClient client, string requestUri,
            long? from = null, long? to = null, bool ensureSuccess = true)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Range = new RangeHeaderValue(from, to);

            using (request)
            {
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);

                if (ensureSuccess)
                    response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            }
        }

        public static async Task<long?> GetContentLengthAsync(this HttpClient client, string requestUri,
            bool ensureSuccess = true)
        {
            using (var response = await client.HeadAsync(requestUri).ConfigureAwait(false))
            {
                if (ensureSuccess)
                    response.EnsureSuccessStatusCode();

                return response.Content.Headers.ContentLength;
            }
        }

        public static SegmentedHttpStream CreateSegmentedStream(this HttpClient httpClient, string url, long length,
            long segmentSize)
        {
            return new SegmentedHttpStream(httpClient, url, length, segmentSize);
        }

        public static bool IsBlank(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsNotBlank(this string str)
        {
            return !string.IsNullOrWhiteSpace(str);
        }

        public static string SubstringUntil(this string str, string sub,
            StringComparison comparison = StringComparison.Ordinal)
        {
            var index = str.IndexOf(sub, comparison);
            return index < 0 ? str : str.Substring(0, index);
        }

        public static string SubstringAfter(this string str, string sub,
            StringComparison comparison = StringComparison.Ordinal)
        {
            var index = str.IndexOf(sub, comparison);
            return index < 0 ? string.Empty : str.Substring(index + sub.Length, str.Length - index - sub.Length);
        }

        public static string StripNonDigit(this string str)
        {
            return Regex.Replace(str, "\\D", "");
        }

        public static double ParseDouble(this string str)
        {
            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
            var format = NumberFormatInfo.InvariantInfo;

            return double.Parse(str, styles, format);
        }

        public static double ParseDoubleOrDefault(this string str, double defaultValue = default(double))
        {
            const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
            var format = NumberFormatInfo.InvariantInfo;

            return double.TryParse(str, styles, format, out var result)
                ? result
                : defaultValue;
        }

        public static int ParseInt(this string str)
        {
            const NumberStyles styles = NumberStyles.AllowThousands;
            var format = NumberFormatInfo.InvariantInfo;

            return int.Parse(str, styles, format);
        }

        public static int ParseIntOrDefault(this string str, int defaultValue = default(int))
        {
            const NumberStyles styles = NumberStyles.AllowThousands;
            var format = NumberFormatInfo.InvariantInfo;

            return int.TryParse(str, styles, format, out var result)
                ? result
                : defaultValue;
        }

        public static long ParseLong(this string str)
        {
            const NumberStyles styles = NumberStyles.AllowThousands;
            var format = NumberFormatInfo.InvariantInfo;

            return long.Parse(str, styles, format);
        }

        public static long ParseLongOrDefault(this string str, long defaultValue = default(long))
        {
            const NumberStyles styles = NumberStyles.AllowThousands;
            var format = NumberFormatInfo.InvariantInfo;

            return long.TryParse(str, styles, format, out var result)
                ? result
                : defaultValue;
        }

        public static DateTimeOffset ParseDateTimeOffset(this string str)
        {
            return DateTimeOffset.Parse(str, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeUniversal);
        }

        public static DateTimeOffset ParseDateTimeOffset(this string str, string format)
        {
            return DateTimeOffset.ParseExact(str, format, DateTimeFormatInfo.InvariantInfo,
                DateTimeStyles.AssumeUniversal);
        }

        public static string Reverse(this string str)
        {
            var sb = new StringBuilder(str.Length);

            for (var i = str.Length - 1; i >= 0; i--)
                sb.Append(str[i]);

            return sb.ToString();
        }

        public static string UrlEncode(this string url)
        {
            return WebUtility.UrlEncode(url);
        }

        public static string UrlDecode(this string url)
        {
            return WebUtility.UrlDecode(url);
        }

        public static string HtmlEncode(this string url)
        {
            return WebUtility.HtmlEncode(url);
        }

        public static string HtmlDecode(this string url)
        {
            return WebUtility.HtmlDecode(url);
        }

        public static string JoinToString<T>(this IEnumerable<T> enumerable, string separator)
        {
            return string.Join(separator, enumerable);
        }

        public static string[] Split(this string input, params string[] separators)
        {
            return input.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        }

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> enumerable)
        {
            return enumerable ?? Enumerable.Empty<T>();
        }

        public static IEnumerable<TSource> Distinct<TSource, TKey>(this IEnumerable<TSource> enumerable,
            Func<TSource, TKey> selector)
        {
            var existing = new HashSet<TKey>();

            foreach (var element in enumerable)
            {
                if (existing.Add(selector(element)))
                    yield return element;
            }
        }

        public static TValue GetOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dic, TKey key,
            TValue defaultValue = default(TValue))
        {
            return dic.TryGetValue(key, out var result) ? result : defaultValue;
        }

        public static XElement StripNamespaces(this XElement element)
        {
            // Original code credit: http://stackoverflow.com/a/1147012

            var result = new XElement(element);
            foreach (var e in result.DescendantsAndSelf())
            {
                e.Name = XNamespace.None.GetName(e.Name.LocalName);
                var attributes = e.Attributes()
                    .Where(a => !a.IsNamespaceDeclaration)
                    .Where(a => a.Name.Namespace != XNamespace.Xml && a.Name.Namespace != XNamespace.Xmlns)
                    .Select(a => new XAttribute(XNamespace.None.GetName(a.Name.LocalName), a.Value));
                e.ReplaceAttributes(attributes);
            }

            return result;
        }

        public static async Task CopyToAsync(this Stream source, Stream destination,
            IProgress<double> progress = null, CancellationToken cancellationToken = default(CancellationToken),
            int bufferSize = 81920)
        {
            var buffer = new byte[bufferSize];

            var totalBytesCopied = 0L;
            int bytesCopied;

            do
            {
                // Read
                bytesCopied = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

                // Write
                await destination.WriteAsync(buffer, 0, bytesCopied, cancellationToken).ConfigureAwait(false);

                // Report progress
                totalBytesCopied += bytesCopied;
                progress?.Report(1.0 * totalBytesCopied / source.Length);
            } while (bytesCopied > 0);
        }
    }


    internal class SegmentedHttpStream : Stream
    {
        private readonly HttpClient _httpClient;
        private readonly string _url;
        private readonly long _segmentSize;

        private Stream _currentStream;
        private long _position;

        public SegmentedHttpStream(HttpClient httpClient, string url, long length, long segmentSize)
        {
            _url = url;
            _httpClient = httpClient;
            Length = length;
            _segmentSize = segmentSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set
            {
                //value.GuardNotNegative(nameof(value));

                if (_position == value) return;

                _position = value;
                ClearCurrentStream();
            }
        }

        private void ClearCurrentStream()
        {
            _currentStream?.Dispose();
            _currentStream = null;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // If full length has been exceeded - return 0
            if (Position >= Length)
                return 0;

            // If current stream is not set - resolve it
            if (_currentStream == null)
            {
                _currentStream = await _httpClient.GetStreamAsync(_url, Position, Position + _segmentSize - 1)
                    .ConfigureAwait(false);
            }

            // Read from current stream
            var bytesRead = await _currentStream.ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);

            // Advance the position (using field directly to avoid clearing stream)
            _position += bytesRead;

            // If no bytes have been read - resolve a new stream
            if (bytesRead == 0)
            {
                // Clear current stream
                ClearCurrentStream();

                // Recursively read again
                bytesRead = await ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count).ConfigureAwait(false).GetAwaiter().GetResult();

        private long GetNewPosition(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return offset;
                case SeekOrigin.Current:
                    return Position + offset;
                case SeekOrigin.End:
                    return Length + offset;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            // Get new position
            var newPosition = GetNewPosition(offset, origin);
            if (newPosition < 0)
                throw new IOException("An attempt was made to move the position before the beginning of the stream.");

            // Change position
            return Position = newPosition;
        }

        #region Not supported

        public override void Flush() => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        #endregion

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                ClearCurrentStream();
        }
    }

    public class VideoUnplayableException : Exception
    {
        /// <summary>
        /// ID of the video.
        /// </summary>
        public string VideoId { get; }

        /// <summary>
        /// Initializes an instance of <see cref="VideoUnplayableException"/>.
        /// </summary>
        public VideoUnplayableException(string videoId, string message)
            : base(message)
        {
            VideoId = videoId;//.GuardNotNull(nameof(videoId));
        }
    }

    public class VideoUnavailableException : Exception
    {
        /// <summary>
        /// ID of the video.
        /// </summary>
        public string VideoId { get; }

        /// <summary>
        /// Initializes an instance of <see cref="VideoUnavailableException"/>.
        /// </summary>
        public VideoUnavailableException(string videoId, string message)
            : base(message)
        {
            VideoId = videoId;//videoId.GuardNotNull(nameof(videoId));
        }
    }

    public class VideoRequiresPurchaseException : VideoUnplayableException
    {
        /// <summary>
        /// ID of the preview video.
        /// </summary>
        public string PreviewVideoId { get; }

        /// <summary>
        /// Initializes an instance of <see cref="VideoRequiresPurchaseException"/>.
        /// </summary>
        public VideoRequiresPurchaseException(string previewVideoId, string videoId, string message)
            : base(videoId, message)
        {
            PreviewVideoId = previewVideoId;//.GuardNotNull(nameof(previewVideoId));
        }
    }

    internal static class Guards
    {
        //[ContractAnnotation("o:null => halt")]
        //public static T GuardNotNull<T>([NoEnumeration] this T o, string argName = null) where T : class
        //{
        //    return o ?? throw new ArgumentNullException(argName);
        //}


        public static T GuardNotNull<T>(this T o, string argName = null) where T : class
        {
            return o ?? throw new ArgumentNullException(argName);
        }

        public static TimeSpan GuardNotNegative(this TimeSpan t, string argName = null)
        {
            return t >= TimeSpan.Zero
                ? t
                : throw new ArgumentOutOfRangeException(argName, t, "Cannot be negative.");
        }

        public static int GuardNotNegative(this int i, string argName = null)
        {
            return i >= 0
                ? i
                : throw new ArgumentOutOfRangeException(argName, i, "Cannot be negative.");
        }

        public static long GuardNotNegative(this long i, string argName = null)
        {
            return i >= 0
                ? i
                : throw new ArgumentOutOfRangeException(argName, i, "Cannot be negative.");
        }

        public static int GuardPositive(this int i, string argName = null)
        {
            return i > 0
                ? i
                : throw new ArgumentOutOfRangeException(argName, i, "Cannot be negative or zero.");
        }

        public static long GuardPositive(this long i, string argName = null)
        {
            return i > 0
                ? i
                : throw new ArgumentOutOfRangeException(argName, i, "Cannot be negative or zero.");
        }
    }


    internal static class UrlEx
    {
        public static string SetQueryParameter(string url, string key, string value)
        {
            value = value ?? string.Empty;

            // Find existing parameter
            var existingMatch = Regex.Match(url, $@"[?&]({Regex.Escape(key)}=?.*?)(?:&|/|$)");

            // Parameter already set to something
            if (existingMatch.Success)
            {
                var group = existingMatch.Groups[1];

                // Remove existing
                url = url.Remove(group.Index, group.Length);

                // Insert new one
                url = url.Insert(group.Index, $"{key}={value}");

                return url;
            }
            // Parameter hasn't been set yet
            else
            {
                // See if there are other parameters
                var hasOtherParams = url.IndexOf('?') >= 0;

                // Prepend either & or ? depending on that
                var separator = hasOtherParams ? '&' : '?';

                // Assemble new query string
                return url + separator + key + '=' + value;
            }
        }

        public static string SetRouteParameter(string url, string key, string value)
        {
            value = value ?? string.Empty;

            // Find existing parameter
            var existingMatch = Regex.Match(url, $@"/({Regex.Escape(key)}/?.*?)(?:/|$)");

            // Parameter already set to something
            if (existingMatch.Success)
            {
                var group = existingMatch.Groups[1];

                // Remove existing
                url = url.Remove(group.Index, group.Length);

                // Insert new one
                url = url.Insert(group.Index, $"{key}/{value}");

                return url;
            }
            // Parameter hasn't been set yet
            else
            {
                // Assemble new query string
                return url + '/' + key + '/' + value;
            }
        }

        public static Dictionary<string, string> SplitQuery(string query)
        {
            var dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var paramsEncoded = query.TrimStart('?').Split("&");
            foreach (var paramEncoded in paramsEncoded)
            {
                var param = paramEncoded.UrlDecode();

                // Look for the equals sign
                var equalsPos = param.IndexOf('=');
                if (equalsPos <= 0)
                    continue;

                // Get the key and value
                var key = param.Substring(0, equalsPos);
                var value = equalsPos < param.Length
                    ? param.Substring(equalsPos + 1)
                    : string.Empty;

                // Add to dictionary
                dic[key] = value;
            }

            return dic;
        }
    }
}
