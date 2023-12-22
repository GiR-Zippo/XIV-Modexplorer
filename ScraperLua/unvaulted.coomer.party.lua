-- unvaulted.coomer.party

-- required output vars
Images = {};
ModName = "";
Downloads = {};
Content = "";

local lines = {}

function GetPictures()
    for _, line in pairs(lines) do
        if string.find(line, '<img fetchpriority=[,"]high[,"] width=') then
            pic = split(line, 'src=\"')[2];
            pic = split(pic, '\"')[1];
            table.insert(Images, pic)
        end
    end
end

function GetContent()
    local des = false;
    for _, line in pairs(lines) do

        if des == true then
            if string.find(line, '</div>') then
                des =false;
            else
                Content = Content .. line
            end
        end
        if string.find(line, '.elementor%-widget%-text%-editor .elementor%-drop%-cap%-letter{display:inline%-block}</style>') then
            des = true;
        end
    end
end

function GetDownload()
    local dl = "";
    for _, line in pairs(lines) do
        if string.find(line, '<a class=[,"]elementor%-button elementor%-button%-link') then
            dl = split(line, 'href=\"')[2]
            dl = split(dl, '\"')[1]
            print(dl)
        end
        if string.find(line, 'fa%-download') then
            table.insert(Downloads, dl);
            dl = "";
        end
    end
end

function main()
    print("unvaulted.coomer.party");
    HtmlData = HtmlData:gsub("[\r]", "") -- strip the \r
    for _, line in pairs(split(HtmlData, "\n")) do
        table.insert(lines, line)
    end

    GetPictures();
    GetContent();
    GetDownload();
end

main();
