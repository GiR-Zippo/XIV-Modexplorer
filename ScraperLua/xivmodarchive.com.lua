-- xivmodarchive.com

-- required output vars
Images = {};
ModName = "";
Downloads = {};
Content = "";
Replaces = {};
ExternalSite = "";

local lines = {}

-- Internal functions.
function GetModName()
    for _, line in pairs(lines) do
        if string.find(line, '<h1 class=[,"]display%-5[,"] style=[,"]font%-size: 2rem[,"]') then
            ModName = split(line, '>')[2]
            ModName = normalizeHtml(split(ModName,"<")[1])
        end
    end
end

function GetPictures()
    for _, line in pairs(lines) do
        if string.find(line, 'mod%-carousel%-image') then
            image = split(line, '[,"]')[4]
            image = split(image,'[,"]')[1]
            table.insert(Images, image)
        end
    end
end

function GetContent()
    i = 0
    for _, line in pairs(lines) do
        if string.find(line, "</div>") and i == -2 then
            i = 0
        end
        if i == -2 then
            Content = line
        end
        if string.find(line, '<div class="px%-2[,"]>') and i == -1 then
            i = -2
        end
        if string.find(line, '<p class=[,"]lead[,"]>Author\'s Comments:</p>') then
            i = -1
        end
    end
end

function GetDownload()
    for _, line in pairs(lines) do
        if string.find(line, ": %[ via <a href=\"") and string.find(line, ">Direct Download</a> %]") and not string.find(line, "</li>") then
            local patt = ": %[ via <a href=\""
            local url = "https://www.xivmodarchive.com" .. string.match(line, patt .. "(.-)\"")
            url = normalizeHtml(url)
            table.insert(Downloads, url)
        elseif string.find(line, ": %[ via <a href=\"") and string.find(line, ">patreon.com</a> %]") and not string.find(line, "</li>") then
            local patt = ": %[ via <a href=\""
            local url = string.match(line, patt .. "(.-)\"")
            url = normalizeHtml(url)
            ExternalSite = url; -- the princess is in another castle
        elseif string.find(line, ": %[ via <a href=\"") and string.find(line, "drive.google.com</a> %]") and not string.find(line, "</li>") then
            local patt = ": %[ via <a href=\""
            local url = string.match(line, patt .. "(.-)\"")
            url = normalizeHtml(url)
            table.insert(Downloads, url)
        elseif string.find(line, ": %[ via <a href=\"") and string.find(line, ">mega.nz</a> %]") and not string.find(line, "</li>") then
            local patt = ": %[ via <a href=\""
            local url = string.match(line, patt .. "(.-)\"")
            url = normalizeHtml(url)
            table.insert(Downloads, url)
        end
    end
end

function GetAffectReplace()
    i = 0
    for _, line in pairs(lines) do
        if (i == 1) then
            line = split(line, '>')[2]
            line = split(line, '<')[1]
            for _, item in pairs(split(line, ',')) do
                rep = normalizeHtml(item)
                rep = string.gsub(rep, '^%s*(.-)%s*$', '%1')
                if string.find(rep, '/') then
                    rep = split(rep, '/')[1]
                end
                table.insert(Replaces, rep)
            end
            return
        end
        if string.find(line, '<div class=[,"]mod%-meta%-block [,"]> Affects / Replaces :') then
            i = 1
        end
    end
end

function main()
    print("xivmodarchive.com");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for _, line in pairs(split(HtmlData, "\n")) do
        table.insert(lines, line)
    end
    GetModName();
    GetPictures();
    GetContent();
    GetDownload();
    GetAffectReplace();
end

main();