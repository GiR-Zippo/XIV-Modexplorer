-- Glamourdresser.com

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
    local des = false;
    for _, line in pairs(lines) do
        if string.find(line, '<h2 class=[,"]o%-blog%-post__title[,"]>') then
            ModName = split(line, '>')[2]
            ModName = split(ModName,"<")[1]
        end
    end
end

function GetPictures()
    local picdata = false;
    local data = "";
    for _, line in pairs(lines) do
        if string.find(line, 'thumbnailUrl[,"]') then
            picdata = true;
            a = split(line, 'thumbnailUrl":"')[2]
            a = split(a, '"')[1]
            a = a:gsub('\\', "") -- remove the backslash
            table.insert(Images, a)
        end
        if picdata == true then
            if string.find(line, '</script>') then
                picdata = false
            end
        end
    end
end

function GetContent()
    local des = false;
    for _, line in pairs(lines) do
        if des == true then
            if string.find(line, '<div data%-elementor%-type=[,"]') then
                des =false;
            else
                Content = Content .. line
            end
        end
        if string.find(line, '<div class=[,"]p%-blog%-single__content h%-clearfix[,"]>') then
            des = true;
        end
    end
end

function GetDownload()
    local des = false;
    for _, line in pairs(lines) do
        if string.find(line, '<a class=[,"]elementor%-button elementor%-button%-link elementor%-size%-lg[,"] href=[,"]') then
            dl = split(line, 'href="')[2]
            if string.find(line, 'glamourdresser.com') then
                table.insert(Downloads, split(dl, '"')[1]);
            elseif string.find(line, 'mega.nz') then
                table.insert(Downloads, split(dl, '"')[1]);
            elseif string.find(line, 'drive.google.com') then
                table.insert(Downloads, split(dl, '"')[1]);
            else
                ExternalSite = split(dl, '"')[1];
            end

        end
    end
end

function GetAffectReplace()
    for _, line in pairs(lines) do
        if string.find(line, 'Affects: ') then
            line = split(line, 'Affects: ')[2]
            line = split(line, '</span>')[1]
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
    end
end

function main()
    print("glamourdresser.com");
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
