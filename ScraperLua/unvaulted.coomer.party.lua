-- unvaulted.coomer.party

-- required output vars
Images = {};
ModName = "";
Downloads = {};
Content = "";

local lines = {}

function GetModName()
    local des = false;
    for _, line in pairs(lines) do
        if string.find(line, 'og%:site_name[,"] content=[,"]') then
            ModName = split(line, 'content=\"')[2];
            ModName = split(ModName, ' - Unvaulted')[1];
        end
    end
end

function GetPictures()
    local previews = false;
    for _, line in pairs(lines) do
        if string.find(line, 'meta property=[,"]og:image[,"] content=[,"]') then
            a = split(line, ' content="')[2]
            a = split(a, '"')[1]
            table.insert(Images, a)
        end
        if string.find(line, 'class=[,"]e%-n%-tabs%-content[,"]') then
            previews = true;
        end

        if (previews) then
            if string.find(line, 'class=[,"]e%-gallery%-item elementor%-gallery%-item elementor%-animated%-content[,"]') then
                pic = split(line, 'href=\"')[2];
                pic = split(pic, '\"')[1];
                table.insert(Images, pic)
            end
        end

        --Remove?
        if string.find(line, '<img fetchpriority=[,"]high[,"]') then
            if string.find(line, 'class=[,"]attachment%-full size%-full wp%-') then
                if string.find(line, 'data%-srcset=') then
                    pic = split(line, 'data-src=\"')[2];
                    pic = split(pic, '\"')[1];
                    table.insert(Images, pic)
                 end
            end
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
        end
        if string.find(line, 'fa%-download') then
            if not string.find(dl, 'https://t.me') then -- don't deal with telegram
			    table.insert(Downloads, dl);
			    dl = "";
				return
		    end
        end
    end
end

function main()
    print("unvaulted.coomer.party");
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
