
-- required output vars
Images = {}
ModName = "";
Downloads = {}
Content = "";
Replaces = {};
ExternalSite = "";

local lines = {}

function GetModName()
    local innerbody = false;
    for _, line in pairs(lines) do
        if string.find(line, '<div class=[,"]trix%-content[,"]>') then
            innerbody = true;
        end
        if string.find(line, '</body>') then
            innerbody = false;
        end

        if innerbody == true then
            -- get modName
            if string.find(line, '<h1>') then
                if ModName == '' then
                    ModName = split(line, '>')[2];
                    ModName = split(ModName, '<')[1];
                end
            end

        end
    end
end

function GetPictures()
    local innerbody = false;
    for _, line in pairs(lines) do
        if string.find(line, '<div class=[,"]trix%-content[,"]>') then
            innerbody = true;
        end
        if string.find(line, '</body>') then
            innerbody = false;
        end

        if innerbody == true then
            -- get pictures
            if string.find(line, '<figure class=[,"]attachment attachment%-%-preview[,"]><img src=[,"]') then
                url = split(line, '"')[4]
                url = url:gsub('&amp;', "&")
                table.insert(Images, url)
            end

        end
    end
end

function GetContent()
    local innerbody = false;
    local innergal = false;
    local desc = false;

    for _, line in pairs(lines) do
        if string.find(line, '<div class=[,"]trix%-content[,"]>') then
            innerbody = true;
        end
        if string.find(line, '</body>') then
            innerbody = false;
        end

        if innerbody == true then
            if desc then
                if string.find(line, 'https://drive.google.com') then
                    dl = split(line, 'https://drive.google.com')[2]
                    dl = split(dl, '"')[1]
                    dl = "https://drive.google.com" .. dl:gsub('&amp;', "&")
                    table.insert(Downloads, dl)
                end
                Content = Content .. line;
            end

            -- get pic gallery
            if string.find(line, '<div class=[,"]attachment%-gallery attachment%-gallery') then
                innergal = true;
            end
            if innergal then
                if string.find(line, '</div>') then
                    innergal = false;
                    desc = true;
                end
            end
        end
    end
end

function GetDownload()
    for _, line in pairs(lines) do
        if string.find(line, '<a class=[,"]doc_preview%-data[,"] href=[,"]') then
            dl = split(line, 'href="')[2]
            dl = split(dl, '"')[1]
            dl = dl:gsub('&amp;', "&")
            Downloads = {}
            table.insert(Downloads, dl)
        end
    end
end


function main()
    print("subscribestar.adult.lua");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for _, line in pairs(split(HtmlData, "\n")) do
        table.insert(lines, line)
    end

    GetModName();
    GetPictures();
    GetContent();
    GetDownload();
end

main();