﻿using System;
using API.Data.Metadata;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;

namespace API.Services;
#nullable enable

public interface IReadingItemService
{
    ComicInfo? GetComicInfo(string filePath);
    int GetNumberOfPages(string filePath, MangaFormat format);
    string GetCoverImage(string filePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default);
    void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1);
    ParserInfo? ParseFile(string path, string rootPath, LibraryType type);
}

public class ReadingItemService : IReadingItemService
{
    private readonly IArchiveService _archiveService;
    private readonly IBookService _bookService;
    private readonly IImageService _imageService;
    private readonly IDirectoryService _directoryService;
    private readonly IDefaultParser _defaultParser;

    public ReadingItemService(IArchiveService archiveService, IBookService bookService, IImageService imageService, IDirectoryService directoryService)
    {
        _archiveService = archiveService;
        _bookService = bookService;
        _imageService = imageService;
        _directoryService = directoryService;

        _defaultParser = new DefaultParser(directoryService);
    }

    /// <summary>
    /// Gets the ComicInfo for the file if it exists. Null otherwise.
    /// </summary>
    /// <param name="filePath">Fully qualified path of file</param>
    /// <returns></returns>
    public ComicInfo? GetComicInfo(string filePath)
    {
        if (Parser.IsEpub(filePath))
        {
            return _bookService.GetComicInfo(filePath);
        }

        if (Parser.IsComicInfoExtension(filePath))
        {
            return _archiveService.GetComicInfo(filePath);
        }

        return null;
    }

    /// <summary>
    /// Processes files found during a library scan.
    /// </summary>
    /// <param name="path">Path of a file</param>
    /// <param name="rootPath"></param>
    /// <param name="type">Library type to determine parsing to perform</param>
    public ParserInfo? ParseFile(string path, string rootPath, LibraryType type)
    {
        var info = Parse(path, rootPath, type);
        if (info == null)
        {
            return null;
        }


        // This catches when original library type is Manga/Comic and when parsing with non
        if (Parser.IsEpub(path) && Parser.ParseVolume(info.Series) != Parser.LooseLeafVolume) // Shouldn't this be info.Volume != DefaultVolume?
        {
            var hasVolumeInTitle = !Parser.ParseVolume(info.Title)
                    .Equals(Parser.LooseLeafVolume);
            var hasVolumeInSeries = !Parser.ParseVolume(info.Series)
                .Equals(Parser.LooseLeafVolume);

            if (string.IsNullOrEmpty(info.ComicInfo?.Volume) && hasVolumeInTitle && (hasVolumeInSeries || string.IsNullOrEmpty(info.Series)))
            {
                // This is likely a light novel for which we can set series from parsed title
                info.Series = Parser.ParseSeries(info.Title);
                info.Volumes = Parser.ParseVolume(info.Title);
            }
            else
            {
                var info2 = _defaultParser.Parse(path, rootPath, LibraryType.Book);
                info.Merge(info2);
            }

        }

        // This is first time ComicInfo is called
        info.ComicInfo = GetComicInfo(path);
        if (info.ComicInfo == null) return info;

        if (!string.IsNullOrEmpty(info.ComicInfo.Volume))
        {
            info.Volumes = info.ComicInfo.Volume;
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Series))
        {
            info.Series = info.ComicInfo.Series.Trim();
        }
        if (!string.IsNullOrEmpty(info.ComicInfo.Number))
        {
            info.Chapters = info.ComicInfo.Number;
        }

        // Patch is SeriesSort from ComicInfo
        if (!string.IsNullOrEmpty(info.ComicInfo.TitleSort))
        {
            info.SeriesSort = info.ComicInfo.TitleSort.Trim();
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.Format) && Parser.HasComicInfoSpecial(info.ComicInfo.Format))
        {
            info.IsSpecial = true;
            info.Chapters = Parser.DefaultChapter;
            info.Volumes = Parser.LooseLeafVolume;
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.SeriesSort))
        {
            info.SeriesSort = info.ComicInfo.SeriesSort.Trim();
        }

        if (!string.IsNullOrEmpty(info.ComicInfo.LocalizedSeries))
        {
            info.LocalizedSeries = info.ComicInfo.LocalizedSeries.Trim();
        }

        return info;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="format"></param>
    /// <returns></returns>
    public int GetNumberOfPages(string filePath, MangaFormat format)
    {
        switch (format)
        {
            case MangaFormat.Archive:
            {
                return _archiveService.GetNumberOfPagesFromArchive(filePath);
            }
            case MangaFormat.Pdf:
            case MangaFormat.Epub:
            {
                return _bookService.GetNumberOfPages(filePath);
            }
            case MangaFormat.Image:
            {
                return 1;
            }
            case MangaFormat.Unknown:
            default:
                return 0;
        }
    }

    public string GetCoverImage(string filePath, string fileName, MangaFormat format, EncodeFormat encodeFormat, CoverImageSize size = CoverImageSize.Default)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }


        return format switch
        {
            MangaFormat.Epub => _bookService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            MangaFormat.Archive => _archiveService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            MangaFormat.Image => _imageService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            MangaFormat.Pdf => _bookService.GetCoverImage(filePath, fileName, _directoryService.CoverImageDirectory, encodeFormat, size),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Extracts the reading item to the target directory using the appropriate method
    /// </summary>
    /// <param name="fileFilePath">File to extract</param>
    /// <param name="targetDirectory">Where to extract to. Will be created if does not exist</param>
    /// <param name="format">Format of the File</param>
    /// <param name="imageCount">If the file is of type image, pass number of files needed. If > 0, will copy the whole directory.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public void Extract(string fileFilePath, string targetDirectory, MangaFormat format, int imageCount = 1)
    {
        switch (format)
        {
            case MangaFormat.Archive:
                _archiveService.ExtractArchive(fileFilePath, targetDirectory);
                break;
            case MangaFormat.Image:
                _imageService.ExtractImages(fileFilePath, targetDirectory, imageCount);
                break;
            case MangaFormat.Pdf:
                _bookService.ExtractPdfImages(fileFilePath, targetDirectory);
                break;
            case MangaFormat.Unknown:
            case MangaFormat.Epub:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    /// <summary>
    /// Parses information out of a file. If file is a book (epub), it will use book metadata regardless of LibraryType
    /// </summary>
    /// <param name="path"></param>
    /// <param name="rootPath"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    private ParserInfo? Parse(string path, string rootPath, LibraryType type)
    {
        return Parser.IsEpub(path) ? _bookService.ParseInfo(path) : _defaultParser.Parse(path, rootPath, type);
    }
}
