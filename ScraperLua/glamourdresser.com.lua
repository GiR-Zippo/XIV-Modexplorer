-- Glamourdresser.com

Images = {}
Download = "";
Content = "";

local lines = {}

-- Internal functions.
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
            Download = split(line, 'href="')[2]
            Download = split(Download, '"')[1]           
        end
    end
end

function main()
    print("glamourdresser.com");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for _, line in pairs(split(HtmlData, "\n")) do
        table.insert(lines, line)
    end

    GetPictures();
    GetContent();
    GetDownload();
end

main();
